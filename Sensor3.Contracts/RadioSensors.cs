namespace Sensor3.Contracts;

public enum ObservationStatus { Available, Unsupported, PermissionRequired, PermissionDenied, Disabled, Throttled, Error }
public sealed record ObservationResult<T>(ObservationStatus Status, T? Value, string? Detail = null);
public sealed record LocationObservation(double Latitude, double Longitude, double? AccuracyMeters, double? AltitudeMeters,
    double? SpeedMetersPerSecond, double? HeadingDegrees, DateTimeOffset TimestampUtc, bool IsMock);
public sealed record WifiObservation(string Ssid, string Bssid, double RssiDbm, double FrequencyMhz, DateTimeOffset ReceivedAtUtc,
    long? NativeTimestampMicroseconds, bool IsCached);
public sealed record BluetoothObservation(string Identifier, double RssiDbm, string AdvertisementHex, DateTimeOffset ReceivedAtUtc);
public interface ILocationProvider { Task<ObservationResult<LocationObservation>> GetAsync(CancellationToken cancellationToken = default); }
public interface IWifiScanner { Task<ObservationResult<IReadOnlyList<WifiObservation>>> ScanAsync(CancellationToken cancellationToken = default); }
public interface IBluetoothScanner { Task<ObservationResult<IReadOnlyList<BluetoothObservation>>> ScanAsync(TimeSpan duration, CancellationToken cancellationToken = default); }
public interface IStepSensorProvider { Task<IReadOnlyList<SensorDescriptor>> DiscoverAsync(CancellationToken cancellationToken = default); }
