using Sensor3.Contracts;

namespace Sensor3.SensorFusion;

public sealed class TimeAlignedVectorBuffer
{
    private readonly List<(double Time, Vector3D Value)> samples = [];
    public bool Add(double seconds, Vector3D value)
    {
        if (!double.IsFinite(seconds) || !value.IsFinite || samples.Count > 0 && seconds <= samples[^1].Time) return false;
        samples.Add((seconds, value)); if (samples.Count > 64) samples.RemoveAt(0); return true;
    }
    public Vector3D? At(double seconds, double maximumAge = .2)
    {
        if (samples.Count == 0) return null;
        var before = samples.FindLastIndex(x => x.Time <= seconds);
        if (before >= 0 && before + 1 < samples.Count && samples[before + 1].Time - samples[before].Time <= maximumAge * 2)
        {
            var a = samples[before]; var b = samples[before + 1];
            return a.Value + (b.Value - a.Value) * ((seconds - a.Time) / (b.Time - a.Time));
        }
        var nearest = samples.MinBy(x => Math.Abs(x.Time - seconds));
        return Math.Abs(nearest.Time - seconds) <= maximumAge ? nearest.Value : null;
    }
    public void Clear() => samples.Clear();
}
