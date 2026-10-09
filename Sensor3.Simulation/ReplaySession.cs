using System.Diagnostics;
using System.Text.Json;
using Sensor3.Contracts;
using Sensor3.Core;
namespace Sensor3.Simulation;
public sealed class ReplayClock : TimeProvider
{
    public DateTimeOffset Now { get; set; }
    public override DateTimeOffset GetUtcNow() => Now;
}
public sealed class ReplaySession : IDisposable
{
    private readonly SensorRecording recording;
    private readonly long jsonBytes;
    private readonly ReplayClock clock = new();
    public AnalysisSession Analysis { get; private set; }
    public int Index { get; private set; }
    public double Seconds => Index == 0 ? 0 : recording.Frames[Index - 1].OffsetSeconds;
    public double Duration => recording.Frames.LastOrDefault()?.OffsetSeconds ?? 0;
    private int classified, correct;
    private double? firstTruthMovement, firstModelMovement;
    private double elapsedMs, cpuMs;
    private GroundTruth? truth;
    public ReplaySession(SensorRecording recording) { RecordingCodec.Validate(recording); this.recording = recording; jsonBytes = JsonSerializer.SerializeToUtf8Bytes(recording).LongLength; Analysis = Create(); }
    private AnalysisSession Create()
    {
        clock.Now = recording.StartedAtUtc;
        var analysis = new AnalysisSession(recording.Catalogue, clock);
        if (recording.KnownStartHeading is { } h) analysis.Steps.SetKnownStartHeading(h);
        if (recording.StartPosition is { } p) analysis.Steps.SetStartPosition(p.X, p.Y);
        analysis.Steps.SetDeclaredForklift(recording.DeclaredForklift); analysis.Steps.SetDeclaredCarrying(recording.DeclaredCarrying);
        if (recording.GpsOrigin is { } gps) analysis.Vehicle.SetGpsOrigin(gps.Y, gps.X);
        if (recording.RadioMap is { } radio) analysis.Radio.ImportMap(JsonSerializer.Serialize(radio));
        return analysis;
    }
    public void AdvanceTo(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        var started = Stopwatch.GetTimestamp(); using var process = Process.GetCurrentProcess(); var cpuStart = process.TotalProcessorTime;
        while (Index < recording.Frames.Count && recording.Frames[Index].OffsetSeconds <= seconds)
        {
            var frame = recording.Frames[Index++]; clock.Now = recording.StartedAtUtc.AddSeconds(frame.OffsetSeconds); Analysis.Receive(frame.Event);
            if (frame.Truth is not { } actual) continue;
            truth = actual;
            if (actual.Activity is { } activity) { classified++; if (Analysis.Steps.GetActivity().Kind == activity) correct++; }
            if (actual.Steps is > 0) firstTruthMovement ??= frame.OffsetSeconds;
            if (Analysis.Steps.GetSnapshot().Total > 0) firstModelMovement ??= frame.OffsetSeconds;
        }
        elapsedMs += Stopwatch.GetElapsedTime(started).TotalMilliseconds; process.Refresh(); cpuMs += (process.TotalProcessorTime - cpuStart).TotalMilliseconds;
    }
    public void Seek(double seconds)
    {
        Analysis.Dispose(); Index = classified = correct = 0; firstTruthMovement = firstModelMovement = null; elapsedMs = cpuMs = 0; truth = null; Analysis = Create(); AdvanceTo(seconds);
    }
    public ComparisonResult Compare()
    {
        var steps = Analysis.Steps.GetSnapshot(); var pdr = Analysis.Steps.GetPosition(); var heading = Analysis.Steps.GetHeading(); var vehicle = Analysis.Vehicle.GetSnapshot();
        var estimatedPosition = recording.DeclaredForklift ? vehicle.Position : pdr.Position;
        var distance = recording.DeclaredForklift ? vehicle.DistanceMeters : pdr.WalkingMeters + pdr.RunningMeters;
        var estimatedHeading = recording.DeclaredForklift ? vehicle.CourseRadians : heading.Confidence > 0 ? heading.Radians : null;
        return new(truth?.Steps is { } expectedSteps ? steps.Total - expectedSteps : null,
            truth?.DistanceMeters is { } expectedDistance ? distance - expectedDistance : null,
            truth?.HeadingRadians is { } expectedHeading && estimatedHeading is { } h ? Math.Atan2(Math.Sin(h - expectedHeading), Math.Cos(h - expectedHeading)) : null,
            truth?.Position is { } actual && estimatedPosition is { } estimate ? Math.Sqrt(Math.Pow(actual.X - estimate.X, 2) + Math.Pow(actual.Y - estimate.Y, 2)) : null,
            classified > 0 ? correct / (double)classified : null,
            firstTruthMovement is { } start && firstModelMovement is { } model ? model - start : null,
            jsonBytes, elapsedMs, cpuMs,
            recording.Synthetic ? "SYNTHETIC: kontrollmodell, inte fältvalidering. CPU är processens CPU-tid under beräkningen; precision inkluderar Unknown/startfönster." : "Inspelat underlag. Fel visas endast där separat ground truth finns. Kvalitet kräver fysisk validering.");
    }
    public void Dispose() => Analysis.Dispose();
}
