using Sensor3.Contracts;
using Sensor3.Sensors;
using Windows.Devices.WiFi;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;
using Microsoft.Extensions.Logging;

namespace Sensor3;

public sealed class WindowsRadioScanner(UpdateSessionGuard guard, NativeObservationLifetime lifetime, ILogger<WindowsRadioScanner> logger) : IWifiScanner, IBluetoothScanner
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public async Task<ObservationResult<IReadOnlyList<WifiObservation>>> ScanAsync(CancellationToken cancellationToken = default)
    {
        using var linked = lifetime.Create(cancellationToken);
        cancellationToken = linked.Token;
        await gate.WaitAsync(cancellationToken);
        try
        {
            using var session = guard.BeginSession();
            if (await WiFiAdapter.RequestAccessAsync() != WiFiAccessStatus.Allowed) return new(ObservationStatus.PermissionDenied, null, "WiFi-/platsåtkomst nekad av Windows.");
            var adapters = await WiFiAdapter.FindAllAdaptersAsync();
            if (adapters.Count == 0) return new(ObservationStatus.Unsupported, null);
            var values = new List<WifiObservation>();
            foreach (var adapter in adapters)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await adapter.ScanAsync().AsTask(cancellationToken);
                values.AddRange(adapter.NetworkReport.AvailableNetworks.Select(x => new WifiObservation(x.Ssid, x.Bssid, x.NetworkRssiInDecibelMilliwatts,
                    x.ChannelCenterFrequencyInKilohertz / 1000.0, adapter.NetworkReport.Timestamp.ToUniversalTime(), null, false)));
            }
            return new(ObservationStatus.Available, RadioObservationRules.UniqueWifi(values));
        }
        catch (UnauthorizedAccessException) { return new(ObservationStatus.PermissionDenied, null, "Windows platsbehörighet krävs för BSSID."); }
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
            var adapter = await Windows.Devices.Bluetooth.BluetoothAdapter.GetDefaultAsync();
            if (adapter is null || !adapter.IsLowEnergySupported) return new(ObservationStatus.Unsupported, null);
            var values = new System.Collections.Concurrent.ConcurrentDictionary<string, BluetoothObservation>();
            var watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Passive };
            void Received(BluetoothLEAdvertisementWatcher _, BluetoothLEAdvertisementReceivedEventArgs args)
            {
                var bytes = new List<byte>();
                foreach (var segment in args.Advertisement.DataSections)
                {
                    using var reader = DataReader.FromBuffer(segment.Data);
                    var data = new byte[reader.UnconsumedBufferLength]; reader.ReadBytes(data);
                    bytes.Add((byte)(data.Length + 1)); bytes.Add(segment.DataType); bytes.AddRange(data);
                }
                var id = args.BluetoothAddress.ToString("X12");
                if (values.Count >= 512 && !values.ContainsKey(id)) return;
                values[id] = new(id, args.RawSignalStrengthInDBm, Convert.ToHexString(bytes.ToArray()), args.Timestamp.ToUniversalTime());
            }
            watcher.Received += Received;
            watcher.Start();
            try { await Task.Delay(duration, cancellationToken); }
            finally { watcher.Stop(); watcher.Received -= Received; }
            return new(watcher.Status == BluetoothLEAdvertisementWatcherStatus.Aborted ? ObservationStatus.Error : ObservationStatus.Available,
                values.Values.OrderBy(x => x.Identifier).ToArray(), "BLE-adresser kan ändras av integritetsskäl; tom skanning är inget bevis på saknad hårdvara.");
        }
        catch (UnauthorizedAccessException) { return new(ObservationStatus.PermissionDenied, null); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) { logger.LogWarning(exception, "BLE-skanning misslyckades"); return new(ObservationStatus.Error, null, exception.Message); }
        finally { gate.Release(); }
    }
}
