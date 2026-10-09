using Sensor3.Contracts;

namespace Sensor3.Positioning;

public sealed record PdrOptions(double WalkingCoefficient = .43, double RunningCoefficient = .65, double CalibrationScale = 1);
public sealed class PedestrianPositionEstimator
{
    private readonly PdrOptions options;
    private readonly List<LocalPoint> path = [];
    private LocalPoint? position;
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
        position = new(x, y); uncertainty = 0; confidence = 0; path.Clear(); path.Add(position); speed = null;
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
            path.Add(position); if (path.Count > 4000) path.RemoveAt(0);
        }
        return length;
    }
    public void Stop() { speed = null; confidence = 0; }
    public PositionSnapshot Snapshot() => new(position, walking, running, speed, uncertainty, confidence, path.ToArray(),
        "Weinberg-baseline: K × amplitud^¼. Kalibrera per person/tempo. Utan riktning hålls XY; osäkerheten växer. Radien är ett heuristiskt mått, inte ett statistiskt konfidensintervall.");
}
