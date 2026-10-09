using Sensor3.SensorFusion;
using Sensor3.Contracts;
using Sensor3.StepDetection;

namespace Sensor3.Core;

public sealed class StepSession : IStepSession, ISensorFusionSession, IDisposable
{
    private readonly object gate = new();
    private readonly ISensorProvider provider;
    private readonly StepOptions options;
    private GaitStepDetector detector;
    private readonly Dictionary<SensorKind, string> sources = [];
    private readonly MotionFusion fusion = new();
    private StepSnapshot snapshot = Empty();
    public StepSession(ISensorProvider provider, StepOptions? options = null)
    {
        this.provider = provider; this.options = options ?? new(); detector = new(this.options); provider.ReadingReceived += Receive; provider.StateChanged += State;
    }
    public void Configure(IReadOnlyList<SensorDescriptor> selected)
    {
        lock (gate)
        {
            sources.Clear(); foreach (var group in selected.GroupBy(x => x.Kind)) sources[group.Key] = group.First().Id;
            detector.ResetEvidence(); fusion.ResetEvidence();
        }
    }
    public StepSnapshot GetSnapshot() { lock (gate) return snapshot; }
    public FusionSnapshot GetFusionSnapshot() { lock (gate) return fusion.GetFusionSnapshot(); }
    public void BeginGyroCalibration() { lock (gate) fusion.BeginGyroCalibration(); }
    public void Reset() { lock (gate) { snapshot = Empty(); detector = new(options); } }
    private static StepSnapshot Empty() => new(0, 0, 0, 0, 0, null, 0, null, "Research-modell. Kalibrera steglängd; gång kan inte bevisas av IMU ensam.");
    private void Receive(SensorReading reading)
    {
        lock (gate)
        {
            if (!sources.TryGetValue(reading.Kind, out var id) || id != reading.SensorId) return;
            var seconds = reading.MonotonicTimestampNanoseconds / 1e9 ?? reading.TimestampUtc?.ToUnixTimeMilliseconds() / 1000d;
            if (seconds is not { } time) return;
            if (reading.Kind == SensorKind.StepCounter)
            { snapshot = snapshot with { NativeTotal = reading.Values.FirstOrDefault()?.Value }; return; }
            var motion = fusion.Process(reading, sources.ContainsKey(SensorKind.LinearAcceleration));
            if (motion is null) return;
            foreach (var step in detector.Update(new(time, motion.FilteredWorldAcceleration.Z, motion.FilteredWorldAcceleration.X,
                motion.FilteredWorldAcceleration.Y, motion.WorldAngularVelocity?.Length ?? 0, motion.Confidence > 0)))
                snapshot = snapshot with { Total = snapshot.Total + 1, Walking = snapshot.Walking + (step.Running ? 0 : 1), Running = snapshot.Running + (step.Running ? 1 : 0),
                    DistanceMeters = snapshot.DistanceMeters + step.LengthMeters, LastStepLengthMeters = step.LengthMeters,
                    Confidence = motion.WorldAngularVelocity is null ? Math.Min(.5, step.Confidence) : step.Confidence,
                    Detail = motion.WorldAngularVelocity is null ? "Gyro saknas: rotationsavvisning är begränsad. Native-total adderas inte." : "Heuristisk gång-/löpningsmodell. Native-total är separat och läggs inte till." };
            snapshot = snapshot with { CadenceHz = detector.Features?.CadenceHz ?? 0 };
        }
    }
    private void State(SensorState state)
    {
        lock (gate) if (sources.Values.Contains(state.SensorId) && state.Status is SensorStatus.Stopped or SensorStatus.Interrupted or SensorStatus.Error)
            snapshot = snapshot with { CadenceHz = 0, Confidence = 0 };
    }
    public void Dispose() { provider.ReadingReceived -= Receive; provider.StateChanged -= State; }
}
