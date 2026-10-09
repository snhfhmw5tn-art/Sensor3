using Sensor3.Contracts;
namespace Sensor3.Core;
public static class SensorEventValidation
{
    public static void Validate(TelemetryEvent value, IReadOnlyList<SensorDescriptor> catalogue)
    {
        if (value is null || new[] { value.Reading is not null, value.State is not null, value.Location is not null, value.Wifi is not null, value.Bluetooth is not null, value.Context is not null }.Count(x => x) != 1) throw new InvalidDataException("Exakt en observation krävs.");
        if (value.Context is { } context) ValidateContext(context);
        static bool Text(string? s, int max) => s is not null && s.Length <= max;
        if (value.Reading is { } r && (!catalogue.Any(x => x.Id == r.SensorId && x.Kind == r.Kind) || !Enum.IsDefined(r.Kind) || !Enum.IsDefined(r.Quality) || !Enum.IsDefined(r.TimestampSource) || !Text(r.CoordinateFrame, 256)
            || r.Values is null || r.Values.Count is < 1 or > 64 || r.Values.Any(v => v is null || !double.IsFinite(v.Value) || !Text(v.Name, 64) || !Text(v.Unit, 32))
            || r.TimestampSource == SensorTimestampSource.AndroidElapsedRealtime && r.MonotonicTimestampNanoseconds is not >= 0 || r.TimestampSource == SensorTimestampSource.WindowsUtc && r.TimestampUtc is null)) throw new InvalidDataException("Ogiltigt sensorprov/källa/klocka.");
        if (value.State is { } s && (!catalogue.Any(x => x.Id == s.SensorId) || !Enum.IsDefined(s.Status) || s.Detail?.Length > 4096)) throw new InvalidDataException("Ogiltig sensorstatus.");
        if (value.Location is { } l && (!double.IsFinite(l.Latitude) || !double.IsFinite(l.Longitude) || l.Latitude is < -90 or > 90 || l.Longitude is < -180 or > 180 || !Finite(l.AccuracyMeters) || l.AccuracyMeters < 0 || !Finite(l.AltitudeMeters) || !Finite(l.SpeedMetersPerSecond) || l.SpeedMetersPerSecond < 0 || !Finite(l.HeadingDegrees) || l.HeadingDegrees is < 0 or > 360)) throw new InvalidDataException("Ogiltig platsfix.");
        if (value.Wifi is { } wifi && (wifi.Count > 512 || wifi.Any(x => x is null || !Text(x.Bssid, 64) || string.IsNullOrWhiteSpace(x.Bssid) || !Text(x.Ssid, 256) || !double.IsFinite(x.RssiDbm) || x.RssiDbm is < -127 or > 0 || !double.IsFinite(x.FrequencyMhz) || x.FrequencyMhz is < 0 or > 7200 || x.NativeTimestampMicroseconds < 0))) throw new InvalidDataException("Ogiltigt WiFi-prov.");
        if (value.Bluetooth is { } ble && (ble.Count > 512 || ble.Any(x => x is null || !Text(x.Identifier, 256) || string.IsNullOrWhiteSpace(x.Identifier) || !double.IsFinite(x.RssiDbm) || x.RssiDbm is < -127 or > 0 || !Text(x.AdvertisementHex, 4096) || x.AdvertisementHex.Length % 2 != 0 || x.AdvertisementHex.Any(c => !Uri.IsHexDigit(c))))) throw new InvalidDataException("Ogiltigt BLE-prov.");
    }
    public static void ValidateContext(AnalysisContext context)
    {
        if (context.DeclaredCarrying is { } carry && !Enum.IsDefined(carry) || context.KnownStartHeading is { } heading && !double.IsFinite(heading)
            || context.StartPosition is { } p && (!double.IsFinite(p.X) || !double.IsFinite(p.Y)) || context.GpsOrigin is { } gps && (!double.IsFinite(gps.X) || !double.IsFinite(gps.Y) || gps.X is < -180 or > 180 || gps.Y is < -85 or > 85)) throw new InvalidDataException("Ogiltig operatörskontext.");
    }
    private static bool Finite(double? value) => value is null || double.IsFinite(value.Value);
}
