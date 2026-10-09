using Windows.Devices.Enumeration;
using Windows.Devices.Sensors;
using Windows.Devices.Sensors.Custom;
using Windows.Foundation;
using Microsoft.Extensions.Logging;
using Sensor3.Contracts;
using Sensor3.Sensors;
using Reading = Sensor3.Contracts.SensorReading;
using N = Sensor3.Sensors.SensorNormalization;
using Accelerometer = Windows.Devices.Sensors.Accelerometer;
using Magnetometer = Windows.Devices.Sensors.Magnetometer;
using OrientationSensor = Windows.Devices.Sensors.OrientationSensor;
using Compass = Windows.Devices.Sensors.Compass;
using Barometer = Windows.Devices.Sensors.Barometer;

namespace Sensor3;

public sealed class WindowsSensorBackend(ILogger<WindowsSensorBackend> logger) : ISensorBackend
{
    private readonly Dictionary<string, Func<SensorSamplingOptions, Action<Reading>, Action<SensorState>, IAsyncDisposable>> sources = [];
    private readonly List<SensorDescriptor> catalogue = [];
    private readonly HashSet<string> nativeIds = [];
    public async Task<IReadOnlyList<SensorDescriptor>> DiscoverAsync(CancellationToken cancellationToken)
    {
        sources.Clear(); catalogue.Clear(); nativeIds.Clear();
        foreach (var type in new[] { AccelerometerReadingType.Standard, AccelerometerReadingType.Linear, AccelerometerReadingType.Gravity })
        {
            var kind = type switch { AccelerometerReadingType.Linear => SensorKind.LinearAcceleration, AccelerometerReadingType.Gravity => SensorKind.Gravity, _ => SensorKind.Accelerometer };
            await AddAsync<Accelerometer, AccelerometerReadingChangedEventArgs>(kind, Accelerometer.GetDeviceSelector(type), async id => await Accelerometer.FromIdAsync(id),
                x => x.MinimumReportInterval, (x, interval) => x.ReportInterval = interval, (x, h) => x.ReadingChanged += h, (x, h) => x.ReadingChanged -= h,
                args => R(kind, args.Reading.Timestamp, Vector("m/s²", N.GToMetersPerSecondSquared(args.Reading.AccelerationX), N.GToMetersPerSecondSquared(args.Reading.AccelerationY), N.GToMetersPerSecondSquared(args.Reading.AccelerationZ))), cancellationToken);
        }
        await AddAsync<Gyrometer, GyrometerReadingChangedEventArgs>(SensorKind.Gyroscope, Gyrometer.GetDeviceSelector(), async id => await Gyrometer.FromIdAsync(id),
            x => x.MinimumReportInterval, (x, i) => x.ReportInterval = i, (x, h) => x.ReadingChanged += h, (x, h) => x.ReadingChanged -= h,
            a => R(SensorKind.Gyroscope, a.Reading.Timestamp, Vector("rad/s", N.DegreesToRadians(a.Reading.AngularVelocityX), N.DegreesToRadians(a.Reading.AngularVelocityY), N.DegreesToRadians(a.Reading.AngularVelocityZ))), cancellationToken);
        await AddAsync<Magnetometer, MagnetometerReadingChangedEventArgs>(SensorKind.Magnetometer, Magnetometer.GetDeviceSelector(), async id => await Magnetometer.FromIdAsync(id),
            x => x.MinimumReportInterval, (x, i) => x.ReportInterval = i, (x, h) => x.ReadingChanged += h, (x, h) => x.ReadingChanged -= h,
            a => R(SensorKind.Magnetometer, a.Reading.Timestamp, Vector("T", N.MicroteslaToTesla(a.Reading.MagneticFieldX), N.MicroteslaToTesla(a.Reading.MagneticFieldY), N.MicroteslaToTesla(a.Reading.MagneticFieldZ)), (int)a.Reading.DirectionalAccuracy switch { 1 => SensorQuality.Unreliable, 2 => SensorQuality.Medium, 3 => SensorQuality.High, _ => SensorQuality.Unknown }), cancellationToken);
        foreach (var readingType in new[] { SensorReadingType.Absolute, SensorReadingType.Relative })
        {
        await AddAsync<OrientationSensor, OrientationSensorReadingChangedEventArgs>(SensorKind.RotationVector, OrientationSensor.GetDeviceSelector(readingType), async id => await OrientationSensor.FromIdAsync(id),
            x => x.MinimumReportInterval, (x, i) => x.ReportInterval = i, (x, h) => x.ReadingChanged += h, (x, h) => x.ReadingChanged -= h,
            a => R(SensorKind.RotationVector, a.Reading.Timestamp, [new("x", a.Reading.Quaternion.X, "1"), new("y", a.Reading.Quaternion.Y, "1"), new("z", a.Reading.Quaternion.Z, "1"), new("w", a.Reading.Quaternion.W, "1")]) with { CoordinateFrame = "Windows " + readingType + " orientation frame" }, cancellationToken, variant: readingType.ToString());
        await AddAsync<Inclinometer, InclinometerReadingChangedEventArgs>(SensorKind.Orientation, Inclinometer.GetDeviceSelector(readingType), async id => await Inclinometer.FromIdAsync(id),
            x => x.MinimumReportInterval, (x, i) => x.ReportInterval = i, (x, h) => x.ReadingChanged += h, (x, h) => x.ReadingChanged -= h,
            a => R(SensorKind.Orientation, a.Reading.Timestamp, [new("pitch", N.DegreesToRadians(a.Reading.PitchDegrees), "rad"), new("roll", N.DegreesToRadians(a.Reading.RollDegrees), "rad"), new("yaw", N.DegreesToRadians(a.Reading.YawDegrees), "rad")]) with { CoordinateFrame = "Windows " + readingType + " orientation frame" }, cancellationToken, variant: readingType.ToString());
        }
        await AddAsync<Compass, CompassReadingChangedEventArgs>(SensorKind.Orientation, Compass.GetDeviceSelector(), async id => await Compass.FromIdAsync(id),
            x => x.MinimumReportInterval, (x, i) => x.ReportInterval = i, (x, h) => x.ReadingChanged += h, (x, h) => x.ReadingChanged -= h,
            a => R(SensorKind.Orientation, a.Reading.Timestamp, a.Reading.HeadingTrueNorth is { } heading
                ? [new("magneticHeading", N.DegreesToRadians(a.Reading.HeadingMagneticNorth), "rad"), new("trueHeading", N.DegreesToRadians(heading), "rad")]
                : [new("magneticHeading", N.DegreesToRadians(a.Reading.HeadingMagneticNorth), "rad")]), cancellationToken);
        await AddAsync<Barometer, BarometerReadingChangedEventArgs>(SensorKind.Barometer, Barometer.GetDeviceSelector(), async id => await Barometer.FromIdAsync(id),
            x => x.MinimumReportInterval, (x, i) => x.ReportInterval = i, (x, h) => x.ReadingChanged += h, (x, h) => x.ReadingChanged -= h,
            a => R(SensorKind.Barometer, a.Reading.Timestamp, [new("pressure", N.HectopascalToPascal(a.Reading.StationPressureInHectopascals), "Pa")]), cancellationToken);
        await AddAsync<LightSensor, LightSensorReadingChangedEventArgs>(SensorKind.Light, LightSensor.GetDeviceSelector(), async id => await LightSensor.FromIdAsync(id),
            x => x.MinimumReportInterval, (x, i) => x.ReportInterval = i, (x, h) => x.ReadingChanged += h, (x, h) => x.ReadingChanged -= h,
            a => R(SensorKind.Light, a.Reading.Timestamp, [new("illuminance", a.Reading.IlluminanceInLux, "lx")]), cancellationToken);
        await AddAsync<Pedometer, PedometerReadingChangedEventArgs>(SensorKind.StepCounter, Pedometer.GetDeviceSelector(), async id => await Pedometer.FromIdAsync(id),
            x => x.MinimumReportInterval, (x, i) => x.ReportInterval = i, (x, h) => x.ReadingChanged += h, (x, h) => x.ReadingChanged -= h,
            a => R(SensorKind.StepCounter, a.Reading.Timestamp, [new("cumulativeSteps", a.Reading.CumulativeSteps, "count"), new("stepKind", (int)a.Reading.StepKind, "category")]), cancellationToken, SensorReportingMode.OnChange);
        await AddAsync<ProximitySensor, ProximitySensorReadingChangedEventArgs>(SensorKind.Proximity, ProximitySensor.GetDeviceSelector(), id => Task.FromResult<ProximitySensor?>(ProximitySensor.FromId(id)),
            _ => 0, (_, _) => { }, (x, h) => x.ReadingChanged += h, (x, h) => x.ReadingChanged -= h,
            a => R(SensorKind.Proximity, a.Reading.Timestamp, a.Reading.DistanceInMillimeters is { } distance
                ? [new("detected", a.Reading.IsDetected ? 1 : 0, "boolean"), new("distance", distance / 1000.0, "m")]
                : [new("detected", a.Reading.IsDetected ? 1 : 0, "boolean")]), cancellationToken, SensorReportingMode.OnChange);
        await AddAsync<SimpleOrientationSensor, SimpleOrientationSensorOrientationChangedEventArgs>(SensorKind.Orientation, SimpleOrientationSensor.GetDeviceSelector(), async id => await SimpleOrientationSensor.FromIdAsync(id),
            _ => 0, (_, _) => { }, (x, h) => x.OrientationChanged += h, (x, h) => x.OrientationChanged -= h,
            a => R(SensorKind.Orientation, a.Timestamp, [new("simpleOrientation", (int)a.Orientation, "category")]), cancellationToken, SensorReportingMode.OnChange);
        await AddAsync<ActivitySensor, ActivitySensorReadingChangedEventArgs>(SensorKind.Activity, ActivitySensor.GetDeviceSelector(), async id => await ActivitySensor.FromIdAsync(id),
            _ => 0, (_, _) => { }, (x, h) => x.ReadingChanged += h, (x, h) => x.ReadingChanged -= h,
            a => R(SensorKind.Activity, a.Reading.Timestamp, [new("activity", (int)a.Reading.Activity, "category"), new("confidence", (int)a.Reading.Confidence, "category")]), cancellationToken, SensorReportingMode.OnChange);
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
            await AddAsync<HingeAngleSensor, HingeAngleSensorReadingChangedEventArgs>(SensorKind.HingeAngle, HingeAngleSensor.GetDeviceSelector(), async id => await HingeAngleSensor.FromIdAsync(id),
                _ => 0, (_, _) => { }, (x, h) => x.ReadingChanged += h, (x, h) => x.ReadingChanged -= h,
                a => R(SensorKind.HingeAngle, a.Reading.Timestamp, [new("angle", N.DegreesToRadians(a.Reading.AngleInDegrees), "rad")]), cancellationToken, SensorReportingMode.OnChange);
        await AddAltimeterAsync(cancellationToken);
        // SDK sensors.h GUID_DEVINTERFACE_SENSOR: discover remaining driver-defined/custom interfaces.
        const string selector = "System.Devices.InterfaceClassGuid:=\"{BA1BB692-9B7A-4833-9A1E-525ED134E7E2}\"";
        await AddAsync<CustomSensor, CustomSensorReadingChangedEventArgs>(SensorKind.Other, selector, async id => nativeIds.Contains(id) ? null : await CustomSensor.FromIdAsync(id),
            x => x.MinimumReportInterval, (x, i) => x.ReportInterval = i, (x, h) => x.ReadingChanged += h, (x, h) => x.ReadingChanged -= h,
            a => R(SensorKind.Other, a.Reading.Timestamp, a.Reading.Properties.Where(x => x.Value is byte or short or ushort or int or uint or long or ulong or float or double or bool)
                .Select(x => new SensorValue(x.Key, Convert.ToDouble(x.Value, System.Globalization.CultureInfo.InvariantCulture), "native")).ToArray()), cancellationToken);
        logger.LogInformation("Windows sensorinventering gav {Count} poster", catalogue.Count);
        return catalogue.ToArray();
    }
    private async Task AddAsync<TSensor, TArgs>(SensorKind kind, string selector, Func<string, Task<TSensor?>> open,
        Func<TSensor, uint> minimum, Action<TSensor, uint> interval, Action<TSensor, TypedEventHandler<TSensor, TArgs>> attach,
        Action<TSensor, TypedEventHandler<TSensor, TArgs>> detach, Func<TArgs, Reading> convert, CancellationToken cancellationToken,
        SensorReportingMode mode = SensorReportingMode.Continuous, string variant = "") where TSensor : class
    {
        try
        {
                foreach (var device in await DeviceInformation.FindAllAsync(selector, new[] { "System.Devices.Manufacturer" }))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (typeof(TSensor) == typeof(CustomSensor) && nativeIds.Contains(device.Id)) continue;
                var id = $"windows:{typeof(TSensor).Name}:{kind}:{variant}:{device.Id}";
                try
                {
                    var sensor = await open(device.Id);
                    if (sensor is null) { catalogue.Add(new(id, device.Id, device.Name, Manufacturer(device), kind, new(mode), SensorStatus.Unsupported, "Drivrutinen exponeras men kan inte öppnas med detta sensor-API.")); continue; }
                    nativeIds.Add(device.Id);
                    var min = minimum(sensor);
                    catalogue.Add(new(id, device.Id, device.Name, Manufacturer(device), kind, new(mode, min > 0 ? 1000.0 / min : null), SensorStatus.Available));
                    sources[id] = (options, reading, state) =>
                    {
                        var subscription = new Subscription<TSensor, TArgs>(sensor, detach);
                        TypedEventHandler<TSensor, TArgs> handler = (_, args) =>
                        {
                            if (subscription.Stopped) return;
                            try { reading(convert(args) with { SensorId = id }); }
                            catch (Exception exception) { logger.LogError(exception, "Sensor {Sensor} läsfel", id); state(new(id, SensorStatus.Error, "Native-läsningen misslyckades.")); }
                        };
                        subscription.Handler = handler;
                        interval(sensor, N.WindowsReportIntervalMilliseconds(options.FrequencyHz, min));
                        try { attach(sensor, handler); } catch { subscription.DisposeAsync().GetAwaiter().GetResult(); throw; }
                        return subscription;
                    };
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                { logger.LogWarning(exception, "Sensor {Sensor} kunde inte öppnas", id); catalogue.Add(new(id, device.Id, device.Name, Manufacturer(device), kind, new(mode), exception is UnauthorizedAccessException ? SensorStatus.PermissionRequired : SensorStatus.Error, exception.Message)); }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { logger.LogWarning(exception, "Inventering av {Kind} misslyckades", kind); catalogue.Add(new("discovery-error:" + typeof(TSensor).Name + kind, "", typeof(TSensor).Name, "", kind, new(mode), SensorStatus.Error, "Sensorinventering misslyckades: " + exception.Message)); }
    }
    private Task AddAltimeterAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var sensor = Altimeter.GetDefault();
            if (sensor is not null)
            {
                var id = "windows:Altimeter:" + sensor.DeviceId;
                nativeIds.Add(sensor.DeviceId);
                catalogue.Add(new(id, sensor.DeviceId, "Altimeter", "Unknown", SensorKind.Altimeter, new(SensorReportingMode.Continuous, sensor.MinimumReportInterval > 0 ? 1000.0 / sensor.MinimumReportInterval : null), SensorStatus.Available));
                sources[id] = (options, reading, _) =>
                {
                    var subscription = new Subscription<Altimeter, AltimeterReadingChangedEventArgs>(sensor, (x, h) => x.ReadingChanged -= h);
                    subscription.Handler = (_, a) => { if (!subscription.Stopped) reading(R(SensorKind.Altimeter, a.Reading.Timestamp, [new("altitudeChange", a.Reading.AltitudeChangeInMeters, "m")]) with { SensorId = id }); };
                    sensor.ReportInterval = N.WindowsReportIntervalMilliseconds(options.FrequencyHz, sensor.MinimumReportInterval);
                    sensor.ReadingChanged += subscription.Handler;
                    return subscription;
                };
            }
        }
        catch (Exception exception) { logger.LogWarning(exception, "Altimeterinventering misslyckades"); }
        return Task.CompletedTask;
    }
    public Task<IAsyncDisposable> SubscribeAsync(SensorDescriptor descriptor, SensorSamplingOptions options, Action<Reading> reading, Action<SensorState> state, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!sources.TryGetValue(descriptor.Id, out var start)) throw new InvalidOperationException("Sensorn är inte tillgänglig.");
        return Task.FromResult(start(options, reading, state));
    }
    private static string Manufacturer(DeviceInformation device) =>
        device.Properties.TryGetValue("System.Devices.Manufacturer", out var value) && value is string name && !string.IsNullOrWhiteSpace(name) ? name : "Unknown";
    private static SensorValue[] Vector(string unit, double x, double y, double z) => [new("x", x, unit), new("y", y, unit), new("z", z, unit)];
    private static Reading R(SensorKind kind, DateTimeOffset timestamp, IReadOnlyList<SensorValue> values, SensorQuality quality = SensorQuality.Unknown) =>
        new("", kind, values, null, timestamp.ToUniversalTime(), DateTimeOffset.UtcNow, SensorTimestampSource.WindowsUtc, quality, "Windows native device/reference frame; no cross-platform frame conversion");
    private sealed class Subscription<TSensor, TArgs>(TSensor sensor, Action<TSensor, TypedEventHandler<TSensor, TArgs>> detach) : IAsyncDisposable
    {
        private int stopped;
        public bool Stopped => Volatile.Read(ref stopped) != 0;
        public TypedEventHandler<TSensor, TArgs>? Handler;
        public ValueTask DisposeAsync() { if (Interlocked.Exchange(ref stopped, 1) == 0 && Handler is not null) detach(sensor, Handler); return ValueTask.CompletedTask; }
    }
}
