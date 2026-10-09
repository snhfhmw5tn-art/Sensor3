namespace Sensor3.Contracts;
public sealed record WifiFingerprint(LocalPoint Point, IReadOnlyList<WifiObservation> Observations, DateTimeOffset CapturedAtUtc, double Quality);
public sealed record BleAnchor(string Identifier, LocalPoint Point, double UncertaintyMeters);
public sealed record RadioMap(IReadOnlyList<WifiFingerprint> Fingerprints, IReadOnlyList<BleAnchor> Beacons);
public sealed record RadioPosition(LocalPoint? Point, double UncertaintyMeters, double Confidence, string Source, string Detail);
public interface IRadioPositionSession
{
    RadioPosition GetRadioPosition();
    void RegisterWifiPoint(double x, double y);
    void RegisterBeacon(string identifier, double x, double y, double uncertaintyMeters);
    string ExportMap();
    void ImportMap(string json);
}
