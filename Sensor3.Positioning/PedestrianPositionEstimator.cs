using Sensor3.Contracts;

namespace Sensor3.Positioning;

public sealed record PdrOptions(double WalkingCoefficient = .43, double RunningCoefficient = .65, double CalibrationScale = 1);
public sealed class PedestrianPositionEstimator
{
    private readonly PdrOptions options;
    private readonly List<LocalPoint> path = [];
    private LocalPoint? position, rawPosition;
    private readonly List<LocalPoint> rawPath = [];
    private double walking, running, uncertainty, confidence, lastSeconds = double.NegativeInfinity;
    private double? speed;
    public PedestrianPositionEstimator(PdrOptions? options = null)
    {
        this.options = options ?? new();
        if (!double.IsFinite(this.options.WalkingCoefficient) || !double.IsFinite(this.options.RunningCoefficient) || !double.IsFinite(this.options.CalibrationScale)
            || this.options.WalkingCoefficient <= 0 || this.options.RunningCoefficient <= 0 || this.options.CalibrationScale <= 0) throw new ArgumentOutOfRangeException(nameof(options));
    }
    public void SetStart(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y)) throw new ArgumentOutOfRangeException(nameof(x));
        position = rawPosition = new(x, y); rawPath.Clear(); rawPath.Add(rawPosition); uncertainty = 0; confidence = 0; path.Clear(); path.Add(position); speed = null;
    }
    public double Add(StepEvent step, HumanHeading heading)
    {
        if (!double.IsFinite(step.Seconds) || step.Seconds <= lastSeconds || !double.IsFinite(step.Amplitude) || step.Amplitude <= 0) return 0;
        lastSeconds = step.Seconds;
        var length = Math.Clamp((step.Running ? options.RunningCoefficient : options.WalkingCoefficient) * options.CalibrationScale * Math.Pow(step.Amplitude, .25), step.Running ? .45 : .25, step.Running ? 2 : 1.2);
        if (step.Running) running += length; else walking += length;
        speed = double.IsFinite(step.IntervalSeconds) && step.IntervalSeconds is > .2 and < 2 ? length / step.IntervalSeconds : null;
        var trusted = heading.Radians is { } angle && double.IsFinite(angle) && heading.Confidence > 0 && double.IsFinite(heading.UncertaintyRadians);
        confidence = trusted ? Math.Min(step.Confidence, heading.Confidence) : 0;
        uncertainty = Math.Sqrt(uncertainty * uncertainty + Math.Pow(length * .2, 2) + Math.Pow(length * (trusted ? Math.Min(Math.PI, heading.UncertaintyRadians) : Math.PI), 2));
        if (position is not null && trusted)
        {
            position = new(position.X + length * Math.Sin(heading.Radians!.Value), position.Y + length * Math.Cos(heading.Radians.Value));
            rawPosition = new(rawPosition!.X + length * Math.Sin(heading.Radians.Value), rawPosition.Y + length * Math.Cos(heading.Radians.Value));
            rawPath.Add(rawPosition); if (rawPath.Count > 4000) rawPath.RemoveAt(0);
            path.Add(position); if (path.Count > 4000) path.RemoveAt(0);
        }
        return length;
    }
    public void Correct(RadioPosition observation)
    {
        if (position is null || observation.Point is not { } point || !double.IsFinite(point.X) || !double.IsFinite(point.Y) || !double.IsFinite(observation.Confidence) || observation.Confidence is <= 0 or > 1 || !double.IsFinite(observation.UncertaintyMeters) || observation.UncertaintyMeters <= 0) return;
        var delta = Math.Sqrt(Math.Pow(position.X - point.X, 2) + Math.Pow(position.Y - point.Y, 2));
        if (delta > Math.Max(20, uncertainty + observation.UncertaintyMeters)) return;
        var weight = Math.Clamp(observation.Confidence, .1, .5);
        position = new(position.X + (point.X - position.X) * weight, position.Y + (point.Y - position.Y) * weight);
        uncertainty = Math.Max(observation.UncertaintyMeters, uncertainty * (1 - weight));
        path.Add(position); if (path.Count > 4000) path.RemoveAt(0);
    }
    public void UpdateActivity(ActivityEstimate activity, double seconds) { if (activity.Stable && activity.Kind == ActivityKind.Stationary) speed = 0; else if (!activity.Stable || activity.Kind is not (ActivityKind.Walking or ActivityKind.Running) || seconds - lastSeconds > 2) speed = null; }
    public void Stop() { speed = null; confidence = 0; }
    public PositionSnapshot Snapshot() => new(position, walking, running, speed, uncertainty, confidence, path.ToArray(),
        "Weinberg-baseline: K × amplitud^¼. Kalibrera per person/tempo. Utan riktning hålls XY; osäkerheten växer. Radien är ett heuristiskt mått, inte ett statistiskt konfidensintervall.", rawPosition, rawPath.ToArray());
}
