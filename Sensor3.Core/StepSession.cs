using Sensor3.SensorFusion;
using Sensor3.Contracts;
using Sensor3.StepDetection;
using Sensor3.ActivityRecognition;
using Sensor3.CarryingMode;

namespace Sensor3.Core;

public sealed class StepSession : IStepSession, ISensorFusionSession, IActivitySession, ICarryingSession, IHumanHeadingSession, IDeviceOrientationEstimator, INavigationSession, IAnalysisContextSource, IDisposable
{
    private Sensor3.Positioning.PedestrianPositionEstimator position = new();
    public event Action<AnalysisContext>? ContextChanged;
    private readonly List<(double Seconds, HumanHeading Heading)> headings = [];
    private readonly object gate = new();
    private readonly ISensorProvider provider;
    private readonly StepOptions options;
    private GaitStepDetector detector;
    private readonly Dictionary<SensorKind, string> sources = [];
    private readonly MotionFusion fusion = new();
    private readonly IHumanHeadingEstimator humanHeading = new AdaptiveHumanHeadingEstimator();
    private readonly IActivityClassifier classifier;
    private readonly Dictionary<int, double> nativeCounters = [];
    private readonly Dictionary<int, double> nativeCounterTimes = [];
    private double lastNativeSeconds = double.NegativeInfinity;
    private long lastNativeArrival;
    private bool nativeAccepting;
    private string? NativeSource => sources.GetValueOrDefault(SensorKind.StepDetector) ?? sources.GetValueOrDefault(SensorKind.StepCounter);
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
            var previousNativeSource = NativeSource;
            headings.Clear(); position.Stop(); sources.Clear(); foreach (var group in selected.GroupBy(x => x.Kind)) sources[group.Key] = group.First().Id;
            if (previousNativeSource != NativeSource || !nativeAccepting) { nativeCounters.Clear(); nativeCounterTimes.Clear(); lastNativeSeconds = double.NegativeInfinity; lastNativeArrival = 0; }
            nativeAccepting = NativeSource is not null;
            snapshot = snapshot with { Source = sources.ContainsKey(SensorKind.StepDetector) ? "Native StepDetector" : sources.ContainsKey(SensorKind.StepCounter) ? "Native StepCounter" : "Unknown", Detail = NativeSource is null ? "Inbyggd stegsensor saknas eller är inte vald. Beräknade steg används inte." : "Inbyggd stegsensor vald; väntar på native-prov. Steglängd och sträcka är uppskattade." };
            detector.ResetEvidence(); fusion.ResetEvidence();
            if (humanHeading.GetHeading().Method != "KnownStart") humanHeading.ResetEvidence();
            this.classifier.Reset(); activity = new(ActivityKind.Unknown, ActivityKind.Unknown, 0, false, 0, "Källorna har ändrats; räknartotal behålls.");
            carryingClassifier.Reset(); carryingContext = carryingContext with { LightLux = null, ProximityMeters = null };
        }
    }
    public bool UsesSensorForPositioning(string sensorId)
    {
        lock (gate)
        {
            var kind = sources.FirstOrDefault(x => x.Value == sensorId).Key;
            if (!sources.TryGetValue(kind, out var selected) || selected != sensorId) return false;
            return kind is SensorKind.StepDetector or SensorKind.LinearAcceleration or SensorKind.Gyroscope or SensorKind.Gravity or SensorKind.RotationVector or SensorKind.Light or SensorKind.Proximity
                || kind == SensorKind.StepCounter && !sources.ContainsKey(SensorKind.StepDetector)
                || kind == SensorKind.Accelerometer && !sources.ContainsKey(SensorKind.LinearAcceleration);
        }
    }
    public void CorrectPosition(RadioPosition observation) { lock (gate) position.Correct(observation); }
    public PositionSnapshot GetPosition() { lock (gate) return position.Snapshot(); }
    public void SetStartPosition(double x, double y) { lock (gate) position.SetStart(x, y); ContextChanged?.Invoke(new(StartPosition: new(x, y))); }
    public AnalysisContext GetAnalysisContext() { lock (gate) return new(context.DeclaredForklift, carryingContext.Declared, humanHeading.GetHeading().Method == "KnownStart" ? humanHeading.GetHeading().Radians : null, position.Snapshot().Position); }
    public StepSnapshot GetSnapshot() { lock (gate) return lastNativeArrival != 0 && System.Diagnostics.Stopwatch.GetElapsedTime(lastNativeArrival).TotalSeconds > 2 ? snapshot with { CadenceHz = 0 } : snapshot; }
    public FusionSnapshot GetFusionSnapshot() { lock (gate) return fusion.GetFusionSnapshot(); }
    public void BeginGyroCalibration() { lock (gate) fusion.BeginGyroCalibration(); }
    public DeviceOrientation? GetDeviceOrientation() { lock (gate) return fusion.GetDeviceOrientation(); }
    public HumanHeading GetHeading() { lock (gate) return humanHeading.GetHeading(); }
    public HeadingComparison GetHeadingComparison() { lock (gate) return humanHeading.GetComparison(); }
    public void SetKnownStartHeading(double radians) { lock (gate) humanHeading.SetKnownStartHeading(radians); ContextChanged?.Invoke(new(KnownStartHeading: radians)); }
    public ActivityEstimate GetActivity() { lock (gate) return activity; }
    public GaitFeatures? GetActivityFeatures() { lock (gate) return detector.Features; }
    public bool IsForkliftDeclared { get { lock (gate) return context.DeclaredForklift; } }
    public CarryingEstimate GetCarrying() { lock (gate) return carrying; }
    public CarryingKind DeclaredCarrying { get { lock (gate) return carryingContext.Declared; } }
    public void SetDeclaredCarrying(CarryingKind kind)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        lock (gate) { carryingContext = carryingContext with { Declared = kind }; carryingClassifier.Reset(); } ContextChanged?.Invoke(new(DeclaredCarrying: kind));
    }
    public void SetDeclaredForklift(bool declared) { lock (gate) { context = context with { DeclaredForklift = declared }; classifier.Reset(); detector.ResetEvidence(); activity = new(ActivityKind.Unknown, declared ? ActivityKind.Forklift : ActivityKind.Unknown, 0, false, 0, "Operatörsuppgift har ändrats; nytt evidensfönster krävs."); } ContextChanged?.Invoke(new(DeclaredForklift: declared)); }
    public void Reset() { lock (gate) { snapshot = Empty() with { Source = snapshot.Source, NativeTotal = snapshot.NativeTotal }; nativeCounters.Clear(); nativeCounterTimes.Clear(); lastNativeArrival = 0; detector = new(options); position = new(); headings.Clear(); } ContextChanged?.Invoke(new(ResetCounters: true)); }
    private static StepSnapshot Empty() => new(0, 0, 0, 0, 0, null, 0, null, "Väntar på inbyggd stegsensor. Beräknad stegräkning används inte.");
    private void Receive(SensorReading reading)
    {
        lock (gate)
        {
            if (!sources.TryGetValue(reading.Kind, out var id) || id != reading.SensorId) return;
            var seconds = reading.MonotonicTimestampNanoseconds / 1e9 ?? (reading.TimestampUtc is { } utc ? (utc - DateTimeOffset.UnixEpoch).TotalSeconds : (double?)null);
            if (seconds is not { } time) return;
            if (reading.Kind is SensorKind.StepCounter or SensorKind.StepDetector) { ReceiveNative(reading, time); return; }
            if (reading.Kind == SensorKind.Light) { carryingContext = carryingContext with { LightLux = reading.Values.FirstOrDefault()?.Value }; return; }
            if (reading.Kind == SensorKind.Proximity) { carryingContext = carryingContext with { ProximityMeters = reading.Values.FirstOrDefault()?.Value }; return; }
            var motion = fusion.Process(reading, sources.ContainsKey(SensorKind.LinearAcceleration));
            if (motion is null) return;
            detector.Update(new(time, motion.FilteredWorldAcceleration.Z, motion.FilteredWorldAcceleration.X,
                motion.FilteredWorldAcceleration.Y, motion.WorldAngularVelocity?.Length ?? 0, motion.Confidence > 0));
            activity = classifier.Update(detector.Features, time, context); position.UpdateActivity(activity, time);
            carrying = carryingClassifier.Update(motion, fusion.GetFusionSnapshot().Orientation, activity, carryingContext);
            var heading = humanHeading.Update(motion, fusion.GetDeviceOrientation(), activity, carrying, detector.Features?.CadenceHz);
            headings.Add((time, heading)); headings.RemoveAll(x => x.Seconds < time - 4); if (headings.Count > 600) headings.RemoveAt(0);
        }
    }
    private void ReceiveNative(SensorReading reading, double time)
    {
        var value = reading.Values.FirstOrDefault(x => x.Unit == "count")?.Value;
        if (value is null || !double.IsFinite(value.Value) || value < 0 || value > 9_000_000_000_000_000 || value != Math.Truncate(value.Value)) return;
        long count;
        if (reading.Kind == SensorKind.StepCounter)
        {
            if (!nativeAccepting) return;
            var categoryValue = reading.TimestampSource == SensorTimestampSource.WindowsUtc ? reading.Values.FirstOrDefault(x => x.Name == "stepKind")?.Value ?? -1 : -1;
            if (!double.IsFinite(categoryValue) || categoryValue != Math.Truncate(categoryValue) || categoryValue is < -1 or > 2) return;
            var category = (int)categoryValue;
            if (time <= nativeCounterTimes.GetValueOrDefault(category, double.NegativeInfinity)) return;
            var previous = nativeCounters.GetValueOrDefault(category, value.Value);
            nativeCounters[category] = value.Value;
            nativeCounterTimes[category] = time;
            snapshot = snapshot with { NativeTotal = nativeCounters.Values.Sum() };
            if (sources.ContainsKey(SensorKind.StepDetector)) return;
            count = (long)Math.Max(0, value.Value - previous);
        }
        else
        {
            if (!nativeAccepting || value != 1 || time <= lastNativeSeconds) return;
            count = 1;
        }
        var interval = time - lastNativeSeconds;
        lastNativeSeconds = Math.Max(lastNativeSeconds, time);
        lastNativeArrival = System.Diagnostics.Stopwatch.GetTimestamp();
        if (count == 0 || context.DeclaredForklift) return;
        if (count > 10_000) { snapshot = snapshot with { Confidence = 0, Detail = "Orimligt hopp i OS-räknaren ignorerat; ny baslinje satt." }; return; }
        var running = activity.Stable && activity.Kind == ActivityKind.Running;
        var walking = activity.Stable && activity.Kind == ActivityKind.Walking;
        var length = running ? options.RunningLength : options.WalkingLength;
        var atStep = headings.LastOrDefault(x => x.Seconds <= time).Heading ?? humanHeading.GetHeading();
        var confidence = reading.Quality switch { SensorQuality.High => 1, SensorQuality.Medium => .6, SensorQuality.Low => .3, _ => 0 };
        position.AddNative(count, time, interval, length, running, atStep, confidence);
        snapshot = snapshot with { Total = checked(snapshot.Total + count), Walking = snapshot.Walking + (walking ? count : 0), Running = snapshot.Running + (running ? count : 0),
            DistanceMeters = snapshot.DistanceMeters + count * length, LastStepLengthMeters = length, Confidence = confidence,
            CadenceHz = double.IsFinite(interval) && interval > .1 && count / interval <= 5 ? count / interval : 0,
            Detail = "Steg från inbyggd OS-sensor. Gång/löpning klassificeras separat; oklassificerade steg ingår i totalen. Steglängd/sträcka är antagna, inte uppmätta. Ingen IMU-stegräkning adderas." };
    }
    private void State(SensorState state)
    {
        lock (gate) if (state.SensorId == NativeSource)
        {
            if (state.Status is SensorStatus.Stopped or SensorStatus.Interrupted or SensorStatus.Error or SensorStatus.PermissionDenied or SensorStatus.PermissionRequired)
            { nativeAccepting = false; nativeCounters.Clear(); nativeCounterTimes.Clear(); }
            else if (state.Status == SensorStatus.Running && !nativeAccepting)
            { nativeAccepting = true; nativeCounters.Clear(); nativeCounterTimes.Clear(); lastNativeSeconds = double.NegativeInfinity; }
        }
        lock (gate) if (sources.Values.Contains(state.SensorId) && state.Status is SensorStatus.Stopped or SensorStatus.Interrupted or SensorStatus.Error)
        { position.Stop(); if (sources.TryGetValue(SensorKind.Light, out var light) && light == state.SensorId) carryingContext = carryingContext with { LightLux = null }; if (sources.TryGetValue(SensorKind.Proximity, out var proximity) && proximity == state.SensorId) carryingContext = carryingContext with { ProximityMeters = null };
          if (sources.TryGetValue(SensorKind.Accelerometer, out var accel) && accel == state.SensorId || sources.TryGetValue(SensorKind.LinearAcceleration, out var linear) && linear == state.SensorId) { fusion.ResetEvidence(); humanHeading.ResetEvidence(); headings.Clear(); carryingClassifier.Reset(); carrying = new(CarryingKind.Unknown, false, 0, new Dictionary<CarryingKind,double> { [CarryingKind.Unknown] = 1 }, 0, 0, "IMU-ström avbruten."); } snapshot = snapshot with { CadenceHz = 0, Confidence = 0 }; classifier.Reset(); activity = new(ActivityKind.Unknown, ActivityKind.Unknown, 0, false, 0, "Sensorström stoppad eller avbruten."); }
    }
    public void Dispose() { provider.ReadingReceived -= Receive; provider.StateChanged -= State; }
}
