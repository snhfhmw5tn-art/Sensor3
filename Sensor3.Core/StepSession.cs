using System.Numerics;
using Sensor3.Contracts;
using Sensor3.StepDetection;

namespace Sensor3.Core;

public sealed class StepSession : IStepSession, IDisposable
{
    private readonly object gate = new();
    private readonly ISensorProvider provider;
    private readonly StepOptions options;
    private GaitStepDetector detector;
    private readonly Dictionary<SensorKind, string> sources = [];
    private Vector3 gravity;
    private double gravityAt = double.NegativeInfinity, gyroAt = double.NegativeInfinity, gyroMagnitude;
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
            detector.ResetEvidence(); gravityAt = gyroAt = double.NegativeInfinity;
        }
    }
    public StepSnapshot GetSnapshot() { lock (gate) return snapshot; }
    public void Reset() { lock (gate) { snapshot = Empty(); detector = new(options); } }
    private static StepSnapshot Empty() => new(0, 0, 0, 0, 0, null, 0, null, "Research-modell. Kalibrera steglängd; gång kan inte bevisas av IMU ensam.");
    private static Vector3 Vector(SensorReading reading) => new((float)(reading.Values.FirstOrDefault(x => x.Name == "x")?.Value ?? 0),
        (float)(reading.Values.FirstOrDefault(x => x.Name == "y")?.Value ?? 0), (float)(reading.Values.FirstOrDefault(x => x.Name == "z")?.Value ?? 0));
    private void Receive(SensorReading reading)
    {
        lock (gate)
        {
            if (!sources.TryGetValue(reading.Kind, out var id) || id != reading.SensorId) return;
            var seconds = reading.MonotonicTimestampNanoseconds / 1e9 ?? reading.TimestampUtc?.ToUnixTimeMilliseconds() / 1000d;
            if (seconds is not { } time) return;
            if (reading.Kind == SensorKind.StepCounter)
            { snapshot = snapshot with { NativeTotal = reading.Values.FirstOrDefault()?.Value }; return; }
            if (reading.Kind == SensorKind.Gyroscope) { gyroMagnitude = Vector(reading).Length(); gyroAt = time; return; }
            if (reading.Kind == SensorKind.Gravity) { gravity = Vector(reading); gravityAt = time; return; }
            if (reading.Kind is not (SensorKind.LinearAcceleration or SensorKind.Accelerometer)) return;
            if (reading.Kind == SensorKind.Accelerometer && sources.ContainsKey(SensorKind.LinearAcceleration)) return;
            var vector = Vector(reading);
            if (!sources.ContainsKey(SensorKind.Gravity) && reading.Kind == SensorKind.Accelerometer)
            {
                var elapsed = double.IsFinite(gravityAt) ? time - gravityAt : .02;
                if (elapsed <= 0) return;
                var alpha = (float)(1 - Math.Exp(-Math.Min(elapsed, .5) * 2 * Math.PI * .3));
                gravity = double.IsFinite(gravityAt) ? Vector3.Lerp(gravity, vector, alpha) : vector; gravityAt = time;
            }
            var reliable = Math.Abs(time - gravityAt) <= .2 && gravity.Length() is > 7 and < 12;
            if (!reliable) { snapshot = snapshot with { Confidence = 0, Detail = "Gravity/acceleration saknas eller är för gamla. Välj gravity + linear acceleration eller accelerometer." }; return; }
            var linear = reading.Kind == SensorKind.Accelerometer ? vector - gravity : vector;
            var vertical = Vector3.Dot(linear, Vector3.Normalize(gravity));
            var horizontal = Math.Sqrt(Math.Max(0, linear.LengthSquared() - vertical * vertical));
            foreach (var step in detector.Update(new(time, vertical, horizontal, 0, Math.Abs(time - gyroAt) <= .2 ? gyroMagnitude : 0, reliable)))
                snapshot = snapshot with { Total = snapshot.Total + 1, Walking = snapshot.Walking + (step.Running ? 0 : 1), Running = snapshot.Running + (step.Running ? 1 : 0),
                    DistanceMeters = snapshot.DistanceMeters + step.LengthMeters, LastStepLengthMeters = step.LengthMeters, Confidence = step.Confidence, Detail = "Heuristisk gång-/löpningsmodell. Native-total är separat och läggs inte till." };
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
