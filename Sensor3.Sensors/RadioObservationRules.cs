using Sensor3.Contracts;

namespace Sensor3.Sensors;

public static class RadioObservationRules
{
    public static void ValidateScanDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.FromSeconds(1) || duration > TimeSpan.FromSeconds(30)) throw new ArgumentOutOfRangeException(nameof(duration));
    }
    public static LocationObservation ValidateLocation(LocationObservation value)
    {
        if (!double.IsFinite(value.Latitude) || value.Latitude is < -90 or > 90 || !double.IsFinite(value.Longitude) || value.Longitude is < -180 or > 180 ||
            value.AccuracyMeters is { } a && (!double.IsFinite(a) || a < 0) || value.SpeedMetersPerSecond is { } s && (!double.IsFinite(s) || s < 0) ||
            value.HeadingDegrees is { } h && (!double.IsFinite(h) || h is < 0 or >= 360) || value.AltitudeMeters is { } z && !double.IsFinite(z))
            throw new InvalidDataException("Ogiltiga native platsvärden.");
        return value;
    }
    public static IReadOnlyList<WifiObservation> UniqueWifi(IEnumerable<WifiObservation> values) => values
        .Where(x => !string.IsNullOrWhiteSpace(x.Bssid) && double.IsFinite(x.RssiDbm) && double.IsFinite(x.FrequencyMhz) && x.FrequencyMhz > 0)
        .GroupBy(x => x.Bssid, StringComparer.OrdinalIgnoreCase).Select(x => x.OrderByDescending(v => v.ReceivedAtUtc).ThenByDescending(v => v.RssiDbm).First()).ToArray();
}
public sealed class NativeStepSensorProvider(ISensorProvider provider) : IStepSensorProvider
{
    public async Task<IReadOnlyList<SensorDescriptor>> DiscoverAsync(CancellationToken cancellationToken = default) =>
        (await provider.DiscoverAsync(cancellationToken)).Where(x => x.Kind is SensorKind.StepCounter or SensorKind.StepDetector).ToArray();
}
