using Sensor3.Contracts;

namespace Sensor3;

public sealed class AndroidSensorPermissions : ISensorPermissionService
{
    public async Task RequestAsync(IReadOnlyCollection<SensorDescriptor> sensors, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(29) && sensors.Any(x => x.Kind is SensorKind.StepCounter or SensorKind.StepDetector))
                await Permissions.RequestAsync<ActivityPermission>();
            if (sensors.Any(x => x.NativeId == "android.sensor.heart_rate")) await Permissions.RequestAsync<HeartRatePermission>();
        });
    }
    private sealed class ActivityPermission : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions => [("android.permission.ACTIVITY_RECOGNITION", true)];
    }
    private sealed class HeartRatePermission : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
            [(OperatingSystem.IsAndroidVersionAtLeast(36) ? "android.permission.health.READ_HEART_RATE" : "android.permission.BODY_SENSORS", true)];
    }
}
