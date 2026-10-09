using System.Text.Json;
using Sensor3.Contracts;
using Sensor3.Positioning;
namespace Sensor3.Core;
public sealed class RadioPositionSession : IRadioPositionSession, IDisposable
{
    private readonly object gate = new();
    private readonly NativeObservationBus bus;
    private readonly INavigationSession navigation;
    private readonly RadioFingerprintEstimator estimator;
    private readonly string? storagePath;
    private readonly TimeProvider clock;
    private DateTimeOffset resultAt;
    private RadioMap map = new([], []);
    private IReadOnlyList<WifiObservation> latestWifi = [];
    private RadioPosition result = new(null, 0, 0, "Unknown", "Registrera kända punkter och skanna.");
    public RadioPositionSession(NativeObservationBus bus, INavigationSession navigation, string? storagePath = null, TimeProvider? timeProvider = null)
    {
        clock = timeProvider ?? TimeProvider.System; estimator = new(timeProvider: clock); this.bus = bus; this.navigation = navigation; this.storagePath = storagePath;
        if (storagePath is not null && File.Exists(storagePath))
        { try { ImportMap(File.ReadAllText(storagePath)); } catch (Exception e) when (e is IOException or JsonException) { result = new(null, 0, 0, "Unknown", "Sparad radiokarta kunde inte läsas. Importera en giltig karta."); } }
        bus.WifiReceived += Wifi; bus.BluetoothReceived += Bluetooth;
    }
    private void Wifi(IReadOnlyList<WifiObservation> values) { lock (gate) { latestWifi = values.ToArray(); result = estimator.Wifi(map, values); resultAt = clock.GetUtcNow(); Correct(); } }
    private void Bluetooth(IReadOnlyList<BluetoothObservation> values) { lock (gate) { result = estimator.Bluetooth(map, values); resultAt = clock.GetUtcNow(); Correct(); } }
    private void Correct() { if (result.Point is not null && result.Confidence > 0) navigation.CorrectPosition(result); }
    public RadioPosition GetRadioPosition() { lock (gate) return result.Point is not null && clock.GetUtcNow() - resultAt > TimeSpan.FromSeconds(30) ? result with { Confidence = 0, Detail = "Unknown: ingen aktuell radioobservation. XY är historisk." } : result; }
    public void RegisterWifiPoint(double x, double y)
    {
        lock (gate)
        {
            var observations = latestWifi.Where(v => !v.IsCached && DateTimeOffset.UtcNow - v.ReceivedAtUtc < TimeSpan.FromSeconds(30)).ToArray();
            var next = map with { Fingerprints = map.Fingerprints.Append(new(new(x, y), observations, DateTimeOffset.UtcNow, .7)).ToArray() };
            RadioFingerprintEstimator.Validate(next); map = next; Save();
        }
    }
    public void RegisterBeacon(string identifier, double x, double y, double uncertaintyMeters)
    {
        lock (gate) { var next = map with { Beacons = map.Beacons.Where(b => !b.Identifier.Equals(identifier, StringComparison.OrdinalIgnoreCase)).Append(new(identifier, new(x, y), uncertaintyMeters)).ToArray() }; RadioFingerprintEstimator.Validate(next); map = next; Save(); }
    }
    public string ExportMap() { lock (gate) return JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true }); }
    public void ImportMap(string json)
    {
        if (json.Length > 4_000_000) throw new InvalidDataException("För stor radiokarta.");
        var next = JsonSerializer.Deserialize<RadioMap>(json) ?? throw new InvalidDataException("Radiokarta saknas."); RadioFingerprintEstimator.Validate(next);
        lock (gate) { map = next; Save(); }
    }
    private void Save() { if (storagePath is null) return; Directory.CreateDirectory(Path.GetDirectoryName(storagePath)!); File.WriteAllText(storagePath + ".tmp", ExportMap()); File.Move(storagePath + ".tmp", storagePath, true); }
    public void Dispose() { bus.WifiReceived -= Wifi; bus.BluetoothReceived -= Bluetooth; }
}
