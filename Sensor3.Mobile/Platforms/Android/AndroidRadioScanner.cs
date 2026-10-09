using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Net.Wifi;
using Sensor3.Contracts;
using Sensor3.Sensors;
using Microsoft.Extensions.Logging;

namespace Sensor3;

public sealed class AndroidRadioScanner(UpdateSessionGuard guard, NativeObservationLifetime lifetime, ILogger<AndroidRadioScanner> logger) : IWifiScanner, IBluetoothScanner
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTimeOffset? lastWifiRequest;
    public async Task<ObservationResult<IReadOnlyList<WifiObservation>>> ScanAsync(CancellationToken cancellationToken = default)
    {
        using var linked = lifetime.Create(cancellationToken);
        cancellationToken = linked.Token;
        await gate.WaitAsync(cancellationToken);
        try
        {
            using var session = guard.BeginSession();
            var allowed = await MainThread.InvokeOnMainThreadAsync(() => Permissions.RequestAsync<Permissions.LocationWhenInUse>());
            if (allowed != PermissionStatus.Granted) return new(ObservationStatus.PermissionDenied, null, "Precis platsbehörighet behövs för WiFi-skanning.");
            var manager = Android.App.Application.Context.GetSystemService(Android.Content.Context.WifiService) as WifiManager;
            if (manager is null) return new(ObservationStatus.Unsupported, null);
            if (!manager.IsWifiEnabled) return new(ObservationStatus.Disabled, null, "WiFi är avstängt.");
            var now = DateTimeOffset.UtcNow;
            // Conservative foreground rate, below Android's four scans per two minutes limit.
            var requested = lastWifiRequest is null || now - lastWifiRequest >= TimeSpan.FromSeconds(35);
            var started = false;
            if (requested)
            {
                lastWifiRequest = now;
#pragma warning disable CA1422
                started = manager.StartScan();
#pragma warning restore CA1422
                if (started) await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
            var results = RadioObservationRules.UniqueWifi((manager.ScanResults ?? []).Select(x => new WifiObservation(Ssid(x), x.Bssid ?? "", x.Level,
                x.Frequency, DateTimeOffset.UtcNow, x.Timestamp, true)));
            return new(started ? ObservationStatus.Available : ObservationStatus.Throttled, results,
                "OS-resultat kan vara cachade. Native elapsed-realtime-tidsstämpel visas; ingen färsk fix garanteras. Minst 35 s mellan begäranden.");
        }
        catch (Java.Lang.SecurityException) { return new(ObservationStatus.PermissionDenied, null, "WiFi kräver precis platsbehörighet och aktiverad platsfunktion."); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) { logger.LogWarning(exception, "WiFi-skanning misslyckades"); return new(ObservationStatus.Error, null, exception.Message); }
        finally { gate.Release(); }
    }
    public async Task<ObservationResult<IReadOnlyList<BluetoothObservation>>> ScanAsync(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        RadioObservationRules.ValidateScanDuration(duration);
        using var linked = lifetime.Create(cancellationToken);
        cancellationToken = linked.Token;
        await gate.WaitAsync(cancellationToken);
        try
        {
            using var session = guard.BeginSession();
            var permission = await MainThread.InvokeOnMainThreadAsync(async () => OperatingSystem.IsAndroidVersionAtLeast(31)
                ? await Permissions.RequestAsync<BlePermission>() : await Permissions.RequestAsync<Permissions.LocationWhenInUse>());
            if (permission != PermissionStatus.Granted) return new(ObservationStatus.PermissionDenied, null);
            var manager = Android.App.Application.Context.GetSystemService(Android.Content.Context.BluetoothService) as BluetoothManager;
            if (manager?.Adapter is not { } adapter) return new(ObservationStatus.Unsupported, null);
            if (!adapter.IsEnabled) return new(ObservationStatus.Disabled, null);
            var scanner = adapter.BluetoothLeScanner;
            if (scanner is null) return new(ObservationStatus.Unsupported, null);
            using var callback = new Callback();
            scanner.StartScan(callback);
            try { await Task.Delay(duration, cancellationToken); }
            finally { scanner.StopScan(callback); }
            if (callback.Error is { } error) return new(ObservationStatus.Error, null, "BLE scan: " + error);
            return new(ObservationStatus.Available, callback.Values.Values.OrderBy(x => x.Identifier).ToArray());
        }
        catch (Java.Lang.SecurityException) { return new(ObservationStatus.PermissionDenied, null); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) { logger.LogWarning(exception, "BLE-skanning misslyckades"); return new(ObservationStatus.Error, null, exception.Message); }
        finally { gate.Release(); }
    }
    private sealed class BlePermission : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
            [("android.permission.BLUETOOTH_SCAN", true), ("android.permission.BLUETOOTH_CONNECT", true), ("android.permission.ACCESS_FINE_LOCATION", true)];
    }
    private static string Ssid(Android.Net.Wifi.ScanResult result)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33)) return result.WifiSsid?.ToString() ?? "";
#pragma warning disable CA1422
        return result.Ssid ?? "";
#pragma warning restore CA1422
    }
    private sealed class Callback : ScanCallback
    {
        public readonly System.Collections.Concurrent.ConcurrentDictionary<string, BluetoothObservation> Values = new();
        public ScanFailure? Error;
        public override void OnScanFailed(ScanFailure errorCode) => Error = errorCode;
        public override void OnScanResult(ScanCallbackType callbackType, Android.Bluetooth.LE.ScanResult? result)
        {
            try
            {
                if (result?.Device?.Address is not { } address || Values.Count >= 512 && !Values.ContainsKey(address)) return;
                Values[address] = new(address, result.Rssi, Convert.ToHexString(result.ScanRecord?.GetBytes() ?? []), DateTimeOffset.UtcNow);
            }
            catch (Java.Lang.SecurityException) { Error = ScanFailure.InternalError; }
        }
    }
}
