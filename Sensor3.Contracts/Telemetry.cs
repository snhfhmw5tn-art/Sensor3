namespace Sensor3.Contracts;

public enum TelemetryMode { Research, Production }
public sealed record TelemetryRegistration(DiagnosticsClient Client, Guid SessionId, TelemetryMode Mode,
    IReadOnlyList<SensorDescriptor> Catalogue, IReadOnlyList<IterationInfo> Iterations, AnalysisContext? InitialContext = null);
public sealed record TelemetryEvent(SensorReading? Reading = null, SensorState? State = null, LocationObservation? Location = null,
    IReadOnlyList<WifiObservation>? Wifi = null, IReadOnlyList<BluetoothObservation>? Bluetooth = null, AnalysisContext? Context = null);
public sealed record TelemetryBatch(Guid SessionId, long Sequence, DateTimeOffset SentAtUtc, IReadOnlyList<TelemetryEvent> Events, long? ClientDroppedEvents = null);
public sealed record TelemetryAcknowledgement(long Sequence, bool Duplicate, BuildInfo ServerBuild);
public sealed record TelemetryStatistics(long Packets, long PayloadBytes, long MissingPackets, long? DroppedEvents,
    double PacketsPerSecond, double PayloadBytesPerSecond, double? RoundTripMilliseconds, DateTimeOffset? LastReceivedAtUtc, string Status);
public sealed record TelemetrySessionSummary(string DeviceId, Guid SessionId, TelemetryMode Mode, BuildInfo ClientBuild, TelemetryStatistics Statistics);
public interface IRealtimeDiagnosticsSource
{
    IReadOnlyList<TelemetrySessionSummary> GetSessions();
    SensorDiagnosticsSnapshot GetDiagnostics(Guid sessionId);
}
public interface ITelemetryClient : IAsyncDisposable
{
    string DeviceId { get; }
    BuildInfo? ServerBuild { get; }
    TelemetryStatistics GetStatistics();
    Task ConnectAsync(Uri endpoint, string token, TelemetryMode mode, CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
