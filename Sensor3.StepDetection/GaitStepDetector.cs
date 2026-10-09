using Sensor3.Contracts;

namespace Sensor3.StepDetection;

public sealed record StepOptions
{
    public double Threshold { get; init; } = .65;
    public double Prominence { get; init; } = 1.1;
    public double MinimumInterval { get; init; } = .26;
    public double MaximumInterval { get; init; } = 1.2;
    public double Periodicity { get; init; } = .48;
    public double WalkingLength { get; init; } = .7;
    public double RunningLength { get; init; } = 1;
    public void Validate()
    {
        if (new[] { Threshold, Prominence, MinimumInterval, MaximumInterval, WalkingLength, RunningLength }.Any(x => !double.IsFinite(x) || x <= 0) ||
            MinimumInterval >= MaximumInterval || !double.IsFinite(Periodicity) || Periodicity is < 0 or > 1 || WalkingLength > 2 || RunningLength > 3)
            throw new ArgumentOutOfRangeException(nameof(StepOptions));
    }
}

// Port of Sensor App 2 ImmediateStepDetector/GaitStepDetector/StepValidator.
// Confidence is a heuristic evidence score, not a calibrated probability.
public sealed class GaitStepDetector
{
    private readonly StepOptions options;
    private readonly List<GaitSample> window = [];
    private readonly List<(double Time, double Amplitude)> candidates = [];
    private double trough, lastApplied = double.NegativeInfinity, lastPeak = double.NegativeInfinity;
    private double? risingSince;
    private double? previousRising;
    private bool armed = true;
    public GaitFeatures? Features { get; private set; }
    public GaitStepDetector(StepOptions? options = null) { this.options = options ?? new(); this.options.Validate(); }
    public void ResetEvidence()
    {
        window.Clear(); candidates.Clear(); trough = 0; risingSince = previousRising = null; armed = true; lastPeak = double.NegativeInfinity; Features = null;
    }
    public IReadOnlyList<StepEvent> Update(GaitSample sample)
    {
        if (!double.IsFinite(sample.Seconds) || new[] { sample.VerticalAcceleration, sample.HorizontalX, sample.HorizontalY, sample.GyroMagnitudeRadians }.Any(x => !double.IsFinite(x))) return [];
        if (window.Count > 0)
        {
            var interval = sample.Seconds - window[^1].Seconds;
            if (interval <= 0) return [];
            if (interval > .5) ResetEvidence();
        }
        window.Add(sample); window.RemoveAll(x => x.Seconds < sample.Seconds - 2);
        while (window.Count > 600) window.RemoveAt(0);
        Features = Extract(window, options.MinimumInterval, options.MaximumInterval);
        trough = Math.Min(trough, sample.VerticalAcceleration);
        if (sample.VerticalAcceleration < .15) { armed = true; risingSince = null; }
        else risingSince ??= sample.Seconds;
        var peakRising = previousRising; previousRising = risingSince;
        if (window.Count >= 3)
        {
            var a = window[^3]; var b = window[^2]; var c = window[^1];
            if (armed && b.VerticalAcceleration > a.VerticalAcceleration && b.VerticalAcceleration >= c.VerticalAcceleration && b.VerticalAcceleration >= options.Threshold &&
                b.VerticalAcceleration - trough >= options.Prominence && b.Seconds - lastPeak + 1e-9 >= .32 && peakRising is { } rising && b.Seconds - rising + 1e-9 >= .06)
            {
                lastPeak = b.Seconds; armed = false;
                var local = window.Where(x => x.Seconds >= b.Seconds - .4).ToArray();
                if (sample.OrientationReliable && local.Average(x => x.HorizontalX * x.HorizontalX + x.HorizontalY * x.HorizontalY) >= .012)
                    candidates.Add((b.Seconds, b.VerticalAcceleration - trough));
                trough = 0;
            }
        }
        candidates.RemoveAll(x => x.Time < sample.Seconds - 3);
        if (Features is not { } f || !f.OrientationReliable || f.SampleRateHz < 15 || f.HorizontalEnergy < .012 || f.HorizontalEnergy < f.Variance * .015 ||
            f.GyroRmsRadians >= 100 * Math.PI / 180 || f.Periodicity < options.Periodicity || candidates.Count < 3) return [];
        var recent = candidates.TakeLast(3).ToArray();
        var intervals = recent.Skip(1).Select((x, i) => x.Time - recent[i].Time).ToArray();
        var mean = intervals.Average();
        if (intervals.Any(x => x < options.MinimumInterval || x > options.MaximumInterval) || Math.Sqrt(intervals.Average(x => Math.Pow(x - mean, 2))) / mean >= .28) return [];
        var running = f.CadenceHz > 2.5 && f.Rms > 2;
        var result = candidates.Where(x => x.Time > lastApplied).Select(x => new StepEvent(x.Time, mean, x.Amplitude, running,
            running ? options.RunningLength : options.WalkingLength, Math.Clamp(.5 + f.Periodicity * .35, .6, .85))).ToArray();
        if (result.Length > 0) lastApplied = result[^1].Seconds;
        return result;
    }
    public static GaitFeatures? Extract(IReadOnlyList<GaitSample> samples, double minimumInterval = .26, double maximumInterval = 1.2)
    {
        if (samples.Count < 3 || samples[^1].Seconds - samples[0].Seconds < 1) return null;
        var duration = samples[^1].Seconds - samples[0].Seconds;
        var mx = samples.Average(x => x.HorizontalX); var my = samples.Average(x => x.HorizontalY);
        var mean = samples.Average(x => x.VerticalAcceleration);
        var uniform = new List<double>(); var index = 0;
        for (var time = samples[0].Seconds; time <= samples[^1].Seconds; time += .02)
        {
            while (index + 1 < samples.Count && samples[index + 1].Seconds < time) index++;
            var a = samples[index]; var b = samples[Math.Min(index + 1, samples.Count - 1)];
            var fraction = a.Seconds == b.Seconds ? 0 : Math.Clamp((time - a.Seconds) / (b.Seconds - a.Seconds), 0, 1);
            uniform.Add(a.VerticalAcceleration + fraction * (b.VerticalAcceleration - a.VerticalAcceleration));
        }
        var uniformMean = uniform.Average();
        var center = uniform.Select(x => x - uniformMean).ToArray(); double correlation = 0; var bestLag = 0;
        for (var lag = (int)Math.Ceiling(minimumInterval * 50); lag <= (int)Math.Floor(maximumInterval * 50) && lag < center.Length / 1.5; lag++)
        {
            double numerator = 0, a = 0, b = 0;
            for (var i = lag; i < center.Length; i++) { numerator += center[i] * center[i - lag]; a += center[i] * center[i]; b += center[i - lag] * center[i - lag]; }
            var value = a * b > 1e-12 ? numerator / Math.Sqrt(a * b) : 0;
            if (value > correlation + .02) { correlation = value; bestLag = lag; }
        }
        return new(Math.Sqrt(samples.Average(x => x.VerticalAcceleration * x.VerticalAcceleration + x.HorizontalX * x.HorizontalX + x.HorizontalY * x.HorizontalY)),
            samples.Average(x => Math.Pow(x.VerticalAcceleration - mean, 2)), samples.Average(x => Math.Pow(x.HorizontalX - mx, 2) + Math.Pow(x.HorizontalY - my, 2)),
            Math.Sqrt(samples.Average(x => x.GyroMagnitudeRadians * x.GyroMagnitudeRadians)), samples.Max(x => x.VerticalAcceleration) - samples.Min(x => x.VerticalAcceleration),
            bestLag > 0 ? 50d / bestLag : 0, correlation, (samples.Count - 1) / duration, samples[^1].OrientationReliable);
    }
}
