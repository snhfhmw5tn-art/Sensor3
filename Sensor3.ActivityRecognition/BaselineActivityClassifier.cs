using Sensor3.Contracts;

namespace Sensor3.ActivityRecognition;

public sealed record ActivityOptions
{
    public double StationaryAccelerationRms { get; init; } = .16;
    public double StationaryGyroRmsRadians { get; init; } = .05;
    public double MinimumGaitRms { get; init; } = .35;
    public double RunningMinimumRms { get; init; } = 2;
    public double RunningCadenceHz { get; init; } = 2.5;
    public double MinimumPeriodicity { get; init; } = .48;
    public double TransitionSeconds { get; init; } = .75;
    public double MinimumStateSeconds { get; init; } = 1;
    public void Validate()
    {
        if (new[] { StationaryAccelerationRms, StationaryGyroRmsRadians, MinimumGaitRms, RunningMinimumRms, RunningCadenceHz, TransitionSeconds, MinimumStateSeconds }.Any(x => !double.IsFinite(x) || x <= 0) ||
            !double.IsFinite(MinimumPeriodicity) || MinimumPeriodicity is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(ActivityOptions));
    }
}

// Reproducible window-feature + temporal baseline; replace through IActivityClassifier for a trained model.
public sealed class BaselineActivityClassifier : IActivityClassifier
{
    private readonly ActivityOptions options;
    private ActivityKind current, candidate;
    private double candidateSince, changedAt = double.NegativeInfinity, previous = double.NegativeInfinity;
    public BaselineActivityClassifier(ActivityOptions? options = null) { this.options = options ?? new(); this.options.Validate(); }
    public void Reset() { current = candidate = ActivityKind.Unknown; previous = changedAt = double.NegativeInfinity; candidateSince = 0; }
    public ActivityEstimate Update(GaitFeatures? features, double seconds, ActivityContext context)
    {
        if (!double.IsFinite(seconds) || seconds < previous) return new(ActivityKind.Unknown, ActivityKind.Unknown, 0, false, seconds, "Ogiltig/oregelbunden tidsordning.");
        if (double.IsFinite(previous) && seconds - previous > .5) Reset();
        previous = seconds;
        var proposal = Classify(features, context);
        if (proposal.Kind == ActivityKind.Unknown)
        { current = candidate = ActivityKind.Unknown; candidateSince = seconds; return new(current, candidate, proposal.Confidence, false, seconds, proposal.Explanation); }
        if (proposal.Kind != candidate) { candidate = proposal.Kind; candidateSince = seconds; }
        if (candidate != current && seconds - candidateSince + 1e-9 >= options.TransitionSeconds && seconds - changedAt >= options.MinimumStateSeconds)
        { current = candidate; changedAt = seconds; }
        return new(current, candidate, proposal.Confidence, current == candidate, seconds, proposal.Explanation);
    }
    private (ActivityKind Kind, double Confidence, string Explanation) Classify(GaitFeatures? features, ActivityContext context)
    {
        if (context.DeclaredForklift)
            return (ActivityKind.Forklift, .65, "Truck deklarerad av operatören. Detta är en prior och ingen IMU-verifierad truckklassificering.");
        if (features is not { } f || !f.OrientationReliable || f.SampleRateHz < 15 ||
            new[] { f.Rms, f.Variance, f.HorizontalEnergy, f.GyroRmsRadians, f.Amplitude, f.CadenceHz, f.Periodicity, f.SampleRateHz }.Any(x => !double.IsFinite(x) || x < 0))
            return (ActivityKind.Unknown, 0, "Otillräckligt tidsfönster, samplingsfrekvens eller sensorunderlag.");
        if (context.TrustedGps && context.GpsSpeedMetersPerSecond is > 2.8 && f.Periodicity < options.MinimumPeriodicity)
            return (ActivityKind.Unknown, .25, "GPS antyder fordonsrörelse; IMU ensam kan inte identifiera trucktyp.");
        if (f.Periodicity >= options.MinimumPeriodicity && f.Amplitude >= 1.1 && f.Rms >= options.MinimumGaitRms && f.HorizontalEnergy >= .012 && f.GyroRmsRadians < 100 * Math.PI / 180)
            return (f.CadenceHz > options.RunningCadenceHz && f.Rms > options.RunningMinimumRms ? ActivityKind.Running : ActivityKind.Walking,
                Math.Clamp(.5 + f.Periodicity * .35, .6, .85), "Periodisk acceleration, energi, kadens och gyro stöder gång/löpning. Kvalitet är heuristisk.");
        if (f.Rms < options.StationaryAccelerationRms && f.GyroRmsRadians < options.StationaryGyroRmsRadians)
            return (ActivityKind.Stationary, .8, "Låg accelerations-/rotationsenergi stöder stillastående; inte kalibrerad sannolikhet.");
        return (ActivityKind.Unknown, .25, "Rörelse finns men saknar tillräcklig aktivitetsevidens.");
    }
}
