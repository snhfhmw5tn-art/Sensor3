namespace Sensor3.Contracts;
public sealed record GroundTruth(long? Steps, double? DistanceMeters, double? HeadingRadians, LocalPoint? Position, ActivityKind? Activity);
public sealed record RecordedFrame(double OffsetSeconds, TelemetryEvent Event, GroundTruth? Truth = null);
public sealed record SensorRecording(int Schema, string Name, bool Synthetic, DateTimeOffset StartedAtUtc, IReadOnlyList<SensorDescriptor> Catalogue,
    IReadOnlyList<RecordedFrame> Frames, bool Truncated = false, double? KnownStartHeading = null, LocalPoint? StartPosition = null,
    bool DeclaredForklift = false, CarryingKind DeclaredCarrying = CarryingKind.Unknown, LocalPoint? GpsOrigin = null, RadioMap? RadioMap = null);
public interface IRecordingSession
{
    bool IsRecording { get; }
    int FrameCount { get; }
    Task StartAsync(string name, CancellationToken cancellationToken = default);
    SensorRecording Stop();
}
public sealed record ComparisonResult(long? StepError, double? DistanceErrorMeters, double? HeadingErrorRadians, double? PositionErrorMeters,
    double? ClassificationPrecision, double? ModelDelaySeconds, long JsonBytes, double ElapsedMilliseconds, double CpuMilliseconds, string Detail);
