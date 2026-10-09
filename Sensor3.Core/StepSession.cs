using Sensor3.SensorFusion;
using Sensor3.Contracts;
using Sensor3.StepDetection;
using Sensor3.ActivityRecognition;
using Sensor3.CarryingMode;

namespace Sensor3.Core;

public sealed class StepSession : IStepSession, ISensorFusionSession, IActivitySession, ICarryingSession, IHumanHeadingSession, IDeviceOrientationEstimator, INavigationSession, IDisposable
{
    private readonly Sensor3.Positioning.PedestrianPositionEstimator position = new();
    private readonly List<(double Seconds, HumanHeading Heading)> headings = [];
    private readonly object gate = new();
    private readonly ISensorProvider provider;
    private readonly StepOptions options;
    private GaitStepDetector detector;
    private readonly Dictionary<SensorKind, string> sources = [];
    private readonly MotionFusion fusion = new();
    private readonly IHumanHeadingEstimator humanHeading = new AdaptiveHumanHeadingEstimator();
    private readonly IActivityClassifier classifier;
    private readonly List<StepEvent> pending = [];
    private ActivityContext context = new();
    private readonly ICarryingClassifier carryingClassifier = new BaselineCarryingClassifier();
    private CarryingContext carryingContext = new();
    private CarryingEstimate carrying = new(CarryingKind.Unknown, false, 0, new Dictionary<CarryingKind, double> { [CarryingKind.Unknown] = 1 }, 0, 0, "Väntar på sensorer.");
    private ActivityEstimate activity = new(ActivityKind.Unknown, ActivityKind.Unknown, 0, false, 0, "Väntar på tillräckligt sensorunderlag.");
    private StepSnapshot snapshot = Empty();
    public StepSession(ISensorProvider provider, StepOptions? options = null, IActivityClassifier? classifier = null)
    {
        this.provider = provider; this.options = options ?? new(); detector = new(this.options); provider.ReadingReceived += Receive; provider.StateChanged += State;
        this.classifier = classifier ?? new BaselineActivityClassifier();
    }
    public void Configure(IReadOnlyList<SensorDescriptor> selected)
    {
        lock (gate)
        {
            headings.Clear(); position.Stop(); sources.Clear(); foreach (var group in selected.GroupBy(x => x.Kind)) sources[group.Key] = group.First().Id;
            detector.ResetEvidence(); fusion.ResetEvidence();
            if (humanHeading.GetHeading().Method != "KnownStart") humanHeading.ResetEvidence();
            this.classifier.Reset(); pending.Clear(); activity = new(ActivityKind.Unknown, ActivityKind.Unknown, 0, false, 0, "Källorna har ändrats; räknartotal behålls.");
            carryingClassifier.Reset(); carryingContext = carryingContext with { LightLux = null, ProximityMeters = null };
        }
    }
    public void CorrectPosition(RadioPosition observation) { lock (gate) position.Correct(observation); }
    public PositionSnapshot GetPosition() { lock (gate) return position.Snapshot(); }
    public void SetStartPosition(double x, double y) { lock (gate) position.SetStart(x, y); }
    public StepSnapshot GetSnapshot() { lock (gate) return snapshot; }
    public FusionSnapshot GetFusionSnapshot() { lock (gate) return fusion.GetFusionSnapshot(); }
    public void BeginGyroCalibration() { lock (gate) fusion.BeginGyroCalibration(); }
    public DeviceOrientation? GetDeviceOrientation() { lock (gate) return fusion.GetDeviceOrientation(); }
    public HumanHeading GetHeading() { lock (gate) return humanHeading.GetHeading(); }
    public HeadingComparison GetHeadingComparison() { lock (gate) return humanHeading.GetComparison(); }
    public void SetKnownStartHeading(double radians) { lock (gate) humanHeading.SetKnownStartHeading(radians); }
    public ActivityEstimate GetActivity() { lock (gate) return activity; }
    public GaitFeatures? GetActivityFeatures() { lock (gate) return detector.Features; }
    public bool IsForkliftDeclared { get { lock (gate) return context.DeclaredForklift; } }
    public CarryingEstimate GetCarrying() { lock (gate) return carrying; }
    public CarryingKind DeclaredCarrying { get { lock (gate) return carryingContext.Declared; } }
    public void SetDeclaredCarrying(CarryingKind kind)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        lock (gate) { carryingContext = carryingContext with { Declared = kind }; carryingClassifier.Reset(); }
    }
    public void SetDeclaredForklift(bool declared) { lock (gate) { context = context with { DeclaredForklift = declared }; pending.Clear(); classifier.Reset(); detector.ResetEvidence(); activity = new(ActivityKind.Unknown, declared ? ActivityKind.Forklift : ActivityKind.Unknown, 0, false, 0, "Operatörsuppgift har ändrats; nytt evidensfönster krävs."); } }
    public void Reset() { lock (gate) { snapshot = Empty(); detector = new(options); pending.Clear(); } }
    private static StepSnapshot Empty() => new(0, 0, 0, 0, 0, null, 0, null, "Research-modell. Kalibrera steglängd; gång kan inte bevisas av IMU ensam.");
    private void Receive(SensorReading reading)
    {
        lock (gate)
        {
            if (!sources.TryGetValue(reading.Kind, out var id) || id != reading.SensorId) return;
            var seconds = reading.MonotonicTimestampNanoseconds / 1e9 ?? (reading.TimestampUtc is { } utc ? (utc - DateTimeOffset.UnixEpoch).TotalSeconds : (double?)null);
            if (seconds is not { } time) return;
            if (reading.Kind == SensorKind.StepCounter)
            { snapshot = snapshot with { NativeTotal = reading.Values.FirstOrDefault()?.Value }; return; }
            if (reading.Kind == SensorKind.Light) { carryingContext = carryingContext with { LightLux = reading.Values.FirstOrDefault()?.Value }; return; }
            if (reading.Kind == SensorKind.Proximity) { carryingContext = carryingContext with { ProximityMeters = reading.Values.FirstOrDefault()?.Value }; return; }
            var motion = fusion.Process(reading, sources.ContainsKey(SensorKind.LinearAcceleration));
            if (motion is null) return;
            var detected = detector.Update(new(time, motion.FilteredWorldAcceleration.Z, motion.FilteredWorldAcceleration.X,
                motion.FilteredWorldAcceleration.Y, motion.WorldAngularVelocity?.Length ?? 0, motion.Confidence > 0));
            activity = classifier.Update(detector.Features, time, context);
            carrying = carryingClassifier.Update(motion, fusion.GetFusionSnapshot().Orientation, activity, carryingContext);
            var heading = humanHeading.Update(motion, fusion.GetDeviceOrientation(), activity, carrying, detector.Features?.CadenceHz);
            headings.Add((time, heading)); headings.RemoveAll(x => x.Seconds < time - 4); if (headings.Count > 600) headings.RemoveAt(0);
            if (activity.Candidate is ActivityKind.Walking or ActivityKind.Running && !context.DeclaredForklift && !carrying.Transition && carrying.Kind is not (CarryingKind.PickingUp or CarryingKind.PuttingAway or CarryingKind.VerticalSwinging)) pending.AddRange(detected);
            else pending.Clear();
            pending.RemoveAll(x => x.Seconds < time - 3);
            if (pending.Count > 64) pending.RemoveRange(0, pending.Count - 64);
            if (activity.Stable && activity.Kind is ActivityKind.Walking or ActivityKind.Running)
            foreach (var step in pending)
            {
                var running = activity.Kind == ActivityKind.Running;
                var atStep = headings.LastOrDefault(x => x.Seconds <= step.Seconds).Heading ?? new HumanHeading(null, 0, Math.PI, "Unknown", "Riktning saknas vid stegets tidpunkt.");
                var length = position.Add(step with { Running = running }, atStep);
                snapshot = snapshot with { Total = snapshot.Total + 1, Walking = snapshot.Walking + (running ? 0 : 1), Running = snapshot.Running + (running ? 1 : 0),
                    DistanceMeters = snapshot.DistanceMeters + length, LastStepLengthMeters = length,
                    Confidence = motion.WorldAngularVelocity is null ? Math.Min(.5, step.Confidence) : step.Confidence,
                    Detail = motion.WorldAngularVelocity is null ? "Gyro saknas: rotationsavvisning är begränsad. Native-total adderas inte." : "Heuristisk gång-/löpningsmodell. Native-total är separat och läggs inte till." };
            }
            if (activity.Stable) pending.Clear();
            snapshot = snapshot with { CadenceHz = detector.Features?.CadenceHz ?? 0 };
        }
    }
    private void State(SensorState state)
    {
        lock (gate) if (sources.Values.Contains(state.SensorId) && state.Status is SensorStatus.Stopped or SensorStatus.Interrupted or SensorStatus.Error)
        { position.Stop(); snapshot = snapshot with { CadenceHz = 0, Confidence = 0 }; pending.Clear(); classifier.Reset(); activity = new(ActivityKind.Unknown, ActivityKind.Unknown, 0, false, 0, "Sensorström stoppad eller avbruten."); }
    }
    public void Dispose() { provider.ReadingReceived -= Receive; provider.StateChanged -= State; }
}
