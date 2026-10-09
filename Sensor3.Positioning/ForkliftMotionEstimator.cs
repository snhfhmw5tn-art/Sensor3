using Sensor3.Contracts;
namespace Sensor3.Positioning;
public sealed record ForkliftOptions(double MaximumAccuracyMeters = 20, double MaximumSpeedMetersPerSecond = 15, double FreshSeconds = 5);
public sealed class ForkliftMotionEstimator : IForkliftMotionEstimator
{
    private readonly TimeProvider clock;
    private readonly ForkliftOptions options;
    private LocalPoint? origin, position, previous;
    private LocationObservation? latest;
    private double distance, confidence;
    private string detail = "Unknown: ange GPS-referens och truckläge; väntar på faktisk platsfix.";
    public ForkliftMotionEstimator(TimeProvider? clock = null, ForkliftOptions? options = null)
    {
        this.clock = clock ?? TimeProvider.System; this.options = options ?? new();
        if (!double.IsFinite(this.options.MaximumAccuracyMeters) || this.options.MaximumAccuracyMeters <= 0 || !double.IsFinite(this.options.MaximumSpeedMetersPerSecond) || this.options.MaximumSpeedMetersPerSecond <= 0 || !double.IsFinite(this.options.FreshSeconds) || this.options.FreshSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(options));
    }
    public void SetGpsOrigin(double latitude, double longitude)
    {
        if (!ValidCoordinates(latitude, longitude)) throw new ArgumentOutOfRangeException(nameof(latitude));
        origin = new(longitude, latitude); position = previous = null; latest = null; distance = confidence = 0;
    }
    private static bool ValidCoordinates(double lat, double lon) => double.IsFinite(lat) && double.IsFinite(lon) && lat is >= -85 and <= 85 && lon is >= -180 and <= 180;
    public void Update(LocationObservation fix, bool declaredForklift, DeviceMotion? motion)
    {
        var age = (clock.GetUtcNow() - fix.TimestampUtc).TotalSeconds;
        if (!declaredForklift || origin is null || fix.IsMock || !ValidCoordinates(fix.Latitude, fix.Longitude) || age < -1 || age > options.FreshSeconds || fix.AccuracyMeters is not { } accuracy || !double.IsFinite(accuracy) || accuracy <= 0 || accuracy > options.MaximumAccuracyMeters
            || fix.SpeedMetersPerSecond is { } invalidSpeed && (!double.IsFinite(invalidSpeed) || invalidSpeed < 0 || invalidSpeed > options.MaximumSpeedMetersPerSecond))
        { latest = null; previous = null; confidence = 0; detail = "Unknown: otillräcklig GPS/referens/truckkontext. Hand-IMU ger ingen trucktranslation."; return; }
        if (latest is not null && fix.TimestampUtc <= latest.TimestampUtc) return;
        var next = new LocalPoint((fix.Longitude - origin.X) * Math.PI / 180 * 6371000 * Math.Cos(origin.Y * Math.PI / 180), (fix.Latitude - origin.Y) * Math.PI / 180 * 6371000);
        if (previous is not null && latest is not null)
        {
            var delta = Math.Sqrt(Math.Pow(next.X - previous.X, 2) + Math.Pow(next.Y - previous.Y, 2));
            var dt = (fix.TimestampUtc - latest.TimestampUtc).TotalSeconds;
            if (dt > options.FreshSeconds || delta > options.MaximumSpeedMetersPerSecond * dt + accuracy + latest.AccuracyMeters)
            { previous = null; confidence = 0; latest = null; detail = "Unknown: GPS-lucka eller för stor innovation; ingen brosträcka adderas."; return; }
            // Suppress stationary GPS wander; movement inside the accuracy envelope is unresolved.
            if (fix.SpeedMetersPerSecond is >= .5 && delta > Math.Max(accuracy, latest.AccuracyMeters!.Value)) distance += delta;
        }
        position = previous = next; latest = fix; confidence = Math.Clamp(1 - accuracy / (options.MaximumAccuracyMeters * 1.25), .1, .8);
        detail = motion is null ? "GPS-baseline; IMU saknas. Sträcka inom GPS-felradien är olöst." : "GPS-position/fart/kurs med kvalitetsgrind. IMU är rörelsekontekst; handacceleration integreras inte till truckposition.";
    }
    public VehicleSnapshot GetSnapshot()
    {
        var fresh = latest is not null && (clock.GetUtcNow() - latest.TimestampUtc).TotalSeconds <= options.FreshSeconds;
        return new(position, distance, fresh ? latest!.SpeedMetersPerSecond : null,
            fresh && latest!.SpeedMetersPerSecond is >= .5 && latest.HeadingDegrees is { } h && double.IsFinite(h) && h is >= 0 and <= 360 ? h * Math.PI / 180 : null,
            latest?.AccuracyMeters, fresh ? confidence : 0, fresh ? detail : "Unknown: ingen aktuell tillförlitlig GPS. Senaste XY är historisk.");
    }
}
