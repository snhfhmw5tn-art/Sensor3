using Sensor3.Contracts;

namespace Sensor3;

public sealed class AndroidSensorPermissions : ISensorPermissionService
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> denied = new();
    public bool WasDenied(string permission) => denied.GetValueOrDefault(permission);
    public async Task RequestAsync(IReadOnlyCollection<SensorDescriptor> sensors, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(29) && sensors.Any(x => x.Kind is SensorKind.StepCounter or SensorKind.StepDetector))
                denied["android.permission.ACTIVITY_RECOGNITION"] = await Permissions.RequestAsync<ActivityPermission>() != PermissionStatus.Granted;
            if (sensors.Any(x => x.NativeId == "android.sensor.heart_rate"))
                denied[HeartRatePermission.PermissionName] = await Permissions.RequestAsync<HeartRatePermission>() != PermissionStatus.Granted;
        });
    }
    private sealed class ActivityPermission : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions => [("android.permission.ACTIVITY_RECOGNITION", true)];
    }
    private sealed class HeartRatePermission : Permissions.BasePlatformPermission
    {
        public static string PermissionName => OperatingSystem.IsAndroidVersionAtLeast(36) ? "android.permission.health.READ_HEART_RATE" : "android.permission.BODY_SENSORS";
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
            [(PermissionName, true)];
    }
}
