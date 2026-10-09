using Sensor3.Contracts;

namespace Sensor3.SensorFusion;

public sealed record HeadingOptions
{
    public double MinimumAnisotropy { get; init; } = .4;
    public double MinimumPhaseCorrelation { get; init; } = .25;
    public double SmoothingSeconds { get; init; } = .2;
    public double StableCarryGyroWeight { get; init; } = .25;
    public void Validate()
    {
        if (!double.IsFinite(MinimumAnisotropy) || MinimumAnisotropy is <= 0 or > 1 || !double.IsFinite(MinimumPhaseCorrelation) || MinimumPhaseCorrelation is <= 0 or > 1 ||
            !double.IsFinite(SmoothingSeconds) || SmoothingSeconds <= 0 || !double.IsFinite(StableCarryGyroWeight) || StableCarryGyroWeight is < 0 or > .5) throw new ArgumentOutOfRangeException(nameof(HeadingOptions));
    }
}
// Research adaptation: world-frame PCA + learned gait-phase sign. It does not reproduce Deng's trained classifier/EKF.
public sealed class AdaptiveHumanHeadingEstimator : IHumanHeadingEstimator
{
    private readonly HeadingOptions options;
    private readonly List<DeviceMotion> window = [];
    private HumanHeading heading = new(null, 0, Math.PI, "Unknown", "Känd startriktning och gångkalibrering krävs.");
    private HeadingComparison comparison = new(null, null, null, 0, 0);
    private double? knownStart, worldOffset, phaseSign, gyroHeading;
    private double previousTime = double.NegativeInfinity;
    private bool needsReanchor;
    public AdaptiveHumanHeadingEstimator(HeadingOptions? options = null) { this.options = options ?? new(); this.options.Validate(); }
    public static double Wrap(double radians) => Math.Atan2(Math.Sin(radians), Math.Cos(radians));
    public static double Difference(double a, double b) => Wrap(a - b);
    public HumanHeading GetHeading() => heading;
    public HeadingComparison GetComparison() => comparison;
    public void SetKnownStartHeading(double radians)
    {
        if (!double.IsFinite(radians)) throw new ArgumentOutOfRangeException(nameof(radians));
        knownStart = Wrap(radians); worldOffset = phaseSign = null; gyroHeading = null; needsReanchor = false;
        window.Clear(); previousTime = double.NegativeInfinity;
        heading = new(knownStart, .5, Math.PI / 4, "KnownStart", "Gå rakt i den angivna startriktningen för gait-alignment. Telefonriktning används inte som personriktning.");
        comparison = new(null, null, knownStart, 0, 0);
    }
    public void ResetEvidence()
    {
        window.Clear(); previousTime = double.NegativeInfinity; worldOffset = phaseSign = gyroHeading = null;
        needsReanchor = knownStart is not null;
        heading = new(null, 0, Math.PI, "Unknown", "Referensramen har avbrutits. Bekräfta startriktning igen.");
        comparison = new(null, null, null, 0, 0);
    }
    public HumanHeading Update(DeviceMotion motion, DeviceOrientation? orientation, ActivityEstimate activity, CarryingEstimate carrying, double? cadenceHz = null)
    {
        if (!double.IsFinite(motion.Seconds) || !motion.FilteredWorldAcceleration.IsFinite || motion.Seconds <= previousTime) return heading;
        var delta = double.IsFinite(previousTime) ? motion.Seconds - previousTime : 0;
        if (delta > .5) { ResetEvidence(); delta = 0; }
        previousTime = motion.Seconds;
        if (motion.WorldAngularVelocity is { } angular && angular.IsFinite && knownStart is { } initial)
            gyroHeading = Wrap((gyroHeading ?? initial) - angular.Z * delta);
        var duration = cadenceHz is > 0 && double.IsFinite(cadenceHz.Value) ? Math.Clamp(2 / cadenceHz.Value, .6, 2) : 2;
        window.Add(motion); window.RemoveAll(x => x.Seconds < motion.Seconds - duration); while (window.Count > 600) window.RemoveAt(0);
        comparison = comparison with { GyroOnlyRadians = gyroHeading };
        if (knownStart is null || needsReanchor)
            return heading = new(null, 0, Math.PI, "Unknown", "Bekräftad känd startriktning saknas.");
        if (carrying.Transition || carrying.Kind is CarryingKind.PickingUp or CarryingKind.PuttingAway or CarryingKind.VerticalSwinging)
            return Hold("Bärpositionsbyte/lyft: föregående riktning hålls utan förflyttningsgrund.", Math.PI / 2);
        if (!activity.Stable || activity.Kind is not (ActivityKind.Walking or ActivityKind.Running) || orientation is null || window.Count < 20 || window[^1].Seconds - window[0].Seconds < duration * .9)
            return Hold("Gait-fönster eller stabil gång/löpning saknas.", Math.PI / 2);
        var mx = window.Average(x => x.FilteredWorldAcceleration.X); var my = window.Average(x => x.FilteredWorldAcceleration.Y); var mz = window.Average(x => x.FilteredWorldAcceleration.Z);
        var xx = window.Average(x => Math.Pow(x.FilteredWorldAcceleration.X - mx, 2));
        var yy = window.Average(x => Math.Pow(x.FilteredWorldAcceleration.Y - my, 2));
        var xy = window.Average(x => (x.FilteredWorldAcceleration.X - mx) * (x.FilteredWorldAcceleration.Y - my));
        var zz = window.Average(x => Math.Pow(x.FilteredWorldAcceleration.Z - mz, 2));
        var anisotropy = Math.Sqrt(Math.Pow(xx - yy, 2) + 4 * xy * xy) / (xx + yy + 1e-12);
        var axisAngle = .5 * Math.Atan2(2 * xy, xx - yy); var ax = Math.Cos(axisAngle); var ay = Math.Sin(axisAngle);
        var axisHeading = Math.Atan2(ax, ay);
        var horizontalVariance = ax * ax * xx + 2 * ax * ay * xy + ay * ay * yy;
        var cross = window.Average(x => ((x.FilteredWorldAcceleration.X - mx) * ax + (x.FilteredWorldAcceleration.Y - my) * ay) * (x.FilteredWorldAcceleration.Z - mz));
        var correlation = horizontalVariance * zz > 1e-12 ? cross / Math.Sqrt(horizontalVariance * zz) : 0;
        comparison = comparison with { Anisotropy = anisotropy, PhaseCorrelation = correlation };
        if (anisotropy < options.MinimumAnisotropy || xx + yy < .001 || Math.Abs(correlation) < options.MinimumPhaseCorrelation)
            return Hold("PCA är svag eller har olöst 180°-ambiguity; accelerationsmönstret räcker inte.", Math.PI);
        if (worldOffset is null)
        {
            worldOffset = Difference(knownStart.Value, axisHeading); phaseSign = Math.Sign(correlation);
        }
        var pca = Wrap(axisHeading + worldOffset.Value);
        var pcaPrior = comparison.PcaRadians ?? knownStart.Value;
        if (Math.Abs(Difference(pca, pcaPrior)) > Math.PI / 2) pca = Wrap(pca + Math.PI);
        var directed = Wrap(axisHeading + worldOffset.Value + (Math.Sign(correlation) == phaseSign ? 0 : Math.PI));
        var stableCarry = carrying.Kind is CarryingKind.Viewing or CarryingKind.HeldStable;
        var target = directed;
        if (stableCarry && heading.Radians is { } stablePrior && motion.WorldAngularVelocity is { } rotation)
        {
            var gyroDelta = -rotation.Z * delta;
            var gaitDelta = Difference(directed, stablePrior);
            if (Math.Abs(gaitDelta) > Math.PI / 180 && gyroDelta * gaitDelta > 0 && Math.Abs(Difference(stablePrior + gyroDelta, directed)) < Math.PI / 6)
                target = Wrap(directed + options.StableCarryGyroWeight * Difference(stablePrior + gyroDelta, directed));
        }
        var alpha = delta > 0 ? 1 - Math.Exp(-delta / options.SmoothingSeconds) : 1;
        var result = heading.Radians is { } previous ? Wrap(previous + alpha * Difference(target, previous)) : target;
        var confidence = Math.Min(.75, .4 + .2 * anisotropy + .15 * Math.Abs(correlation));
        if (motion.WorldAngularVelocity is null) confidence = Math.Min(confidence, .5);
        if (carrying.Kind is CarryingKind.Unknown or CarryingKind.HandRotating or CarryingKind.Scanning) confidence = Math.Min(confidence, .6);
        var uncertainty = (1 - confidence) * Math.PI / 2;
        heading = new(result, confidence, uncertainty, stableCarry ? "Gait-supported gyro/PCA" : "WorldPCA+GaitPhase",
            "Personriktning hämtas från accelerationsaxel och kalibrerad gait-fas. Phone yaw och gyro-only visas enbart som jämförelse. Gait-fas kan ändras med bärare/tempo.");
        comparison = new(pca, gyroHeading, result, anisotropy, correlation);
        return heading;
    }
    private HumanHeading Hold(string detail, double uncertainty)
    {
        heading = heading with { Confidence = 0, UncertaintyRadians = Math.Max(heading.UncertaintyRadians, uncertainty), Method = "Pending/Unknown", Detail = detail };
        comparison = comparison with { AdaptiveRadians = heading.Radians };
        return heading;
    }
}
