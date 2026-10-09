using Android.Hardware;
using Microsoft.Extensions.Logging;
using Sensor3.Contracts;
using Sensor3.Sensors;
using ReportingMode = Sensor3.Contracts.SensorReportingMode;
using SensorStatus = Sensor3.Contracts.SensorStatus;
using SensorStatusAndroid = Android.Hardware.SensorStatus;

namespace Sensor3;

public sealed class AndroidSensorBackend(ILogger<AndroidSensorBackend> logger, AndroidSensorPermissions permissions) : ISensorBackend
{
    private readonly SensorManager manager = (SensorManager)Android.App.Application.Context.GetSystemService(Android.Content.Context.SensorService)!;
    private readonly Dictionary<string, Sensor> native = [];
    public Task<IReadOnlyList<SensorDescriptor>> DiscoverAsync(CancellationToken cancellationToken)
    {
        native.Clear();
        var found = new List<SensorDescriptor>();
        var index = 0;
        foreach (var sensor in manager.GetSensorList(SensorType.All) ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            var type = (int)sensor.Type;
            var id = $"android:{sensor.Id}:{type}:{index++}";
            native[id] = sensor;
            var permission = RequiredPermission(type);
            var permitted = permission is null || AndroidX.Core.Content.ContextCompat.CheckSelfPermission(Android.App.Application.Context, permission) == Android.Content.PM.Permission.Granted;
            var mode = (int)sensor.ReportingMode switch { 1 => ReportingMode.OnChange, 2 => ReportingMode.OneShot, 3 => ReportingMode.SpecialTrigger, _ => ReportingMode.Continuous };
            found.Add(new(id, sensor.StringType ?? type.ToString(), sensor.Name ?? id, sensor.Vendor ?? "Unknown", Kind(type),
                new(mode, sensor.MinDelay > 0 ? 1_000_000.0 / sensor.MinDelay : null, permission is not null),
                permitted ? SensorStatus.Available : permissions.WasDenied(permission!) ? SensorStatus.PermissionDenied : SensorStatus.PermissionRequired,
                permitted ? null : (permissions.WasDenied(permission!) ? "Behörighet nekad: " : "Behörighet krävs: ") + permission));
        }
        logger.LogInformation("Android exponerar {Count} sensorer", found.Count);
        return Task.FromResult<IReadOnlyList<SensorDescriptor>>(found);
    }
    public Task<IAsyncDisposable> SubscribeAsync(SensorDescriptor descriptor, SensorSamplingOptions options, Action<SensorReading> reading, Action<SensorState> state, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!native.TryGetValue(descriptor.Id, out var sensor)) throw new InvalidOperationException("Sensorn har kopplats bort.");
        IAsyncDisposable subscription;
        if (descriptor.Capability.ReportingMode == ReportingMode.OneShot)
        {
            var listener = new TriggerListener(manager, sensor, descriptor, reading, state, logger);
            if (!manager.RequestTriggerSensor(listener, sensor)) { listener.Dispose(); throw new InvalidOperationException("Trigger-sensorn kunde inte startas."); }
            subscription = listener;
        }
        else
        {
            var listener = new Listener(manager, descriptor, reading, logger);
            if (!manager.RegisterListener(listener, sensor, (SensorDelay)SensorNormalization.AndroidSamplingPeriodMicroseconds(options.FrequencyHz)))
            { listener.Dispose(); throw new InvalidOperationException("Sensorn kunde inte registreras."); }
            subscription = listener;
        }
        return Task.FromResult(subscription);
    }
    private static SensorKind Kind(int type) => type switch
    {
        1 or 35 => SensorKind.Accelerometer, 4 or 16 => SensorKind.Gyroscope, 9 => SensorKind.Gravity, 10 => SensorKind.LinearAcceleration,
        2 or 14 => SensorKind.Magnetometer, 11 or 15 or 20 => SensorKind.RotationVector, 3 => SensorKind.Orientation,
        6 => SensorKind.Barometer, 18 => SensorKind.StepDetector, 19 => SensorKind.StepCounter, 5 => SensorKind.Light,
        8 => SensorKind.Proximity, 36 => SensorKind.HingeAngle, _ => SensorKind.Other
    };
    private static string? RequiredPermission(int type) => type is 18 or 19 && OperatingSystem.IsAndroidVersionAtLeast(29)
        ? "android.permission.ACTIVITY_RECOGNITION" : type == 21 ? (OperatingSystem.IsAndroidVersionAtLeast(36) ? "android.permission.health.READ_HEART_RATE" : "android.permission.BODY_SENSORS") : null;
    private static SensorReading MakeReading(SensorDescriptor sensor, long timestamp, IList<float>? values, SensorQuality quality) =>
        new(sensor.Id, sensor.Kind, SensorNormalization.AndroidValues((int.Parse(sensor.Id.Split(':')[2])), values?.ToArray() ?? []),
            timestamp, null, DateTimeOffset.UtcNow, SensorTimestampSource.AndroidElapsedRealtime, quality, "Android device axes; no world-frame conversion");

    private sealed class Listener(SensorManager manager, SensorDescriptor descriptor, Action<SensorReading> reading, ILogger logger)
        : Java.Lang.Object, ISensorEventListener, IAsyncDisposable
    {
        private int stopped;
        public void OnAccuracyChanged(Sensor? sensor, SensorStatusAndroid accuracy) { }
        public void OnSensorChanged(SensorEvent? e)
        {
            if (Volatile.Read(ref stopped) != 0 || e is null) return;
            try { reading(MakeReading(descriptor, e.Timestamp, e.Values, SensorNormalization.AndroidQuality((int)e.Accuracy))); }
            catch (Exception exception) { logger.LogError(exception, "Android sensor-event misslyckades"); }
        }
        public ValueTask DisposeAsync() { if (Interlocked.Exchange(ref stopped, 1) == 0) { manager.UnregisterListener(this); Dispose(); } return ValueTask.CompletedTask; }
    }
    private sealed class TriggerListener(SensorManager manager, Sensor sensor, SensorDescriptor descriptor, Action<SensorReading> reading,
        Action<SensorState> state, ILogger logger) : TriggerEventListener, IAsyncDisposable
    {
        private int stopped;
        public override void OnTrigger(TriggerEvent? e)
        {
            if (Volatile.Read(ref stopped) != 0 || e is null) return;
            try
            {
                reading(MakeReading(descriptor, e.Timestamp, e.Values, SensorQuality.Unknown));
                state(new(descriptor.Id, SensorStatus.Stopped, "Engångstrigger utlöst. Starta en ny session för att aktivera igen."));
            }
            catch (Exception exception) { logger.LogError(exception, "Android trigger misslyckades"); }
        }
        public ValueTask DisposeAsync() { if (Interlocked.Exchange(ref stopped, 1) == 0) { manager.CancelTriggerSensor(this, sensor); Dispose(); } return ValueTask.CompletedTask; }
    }
}
