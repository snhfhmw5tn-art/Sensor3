namespace Sensor3.Contracts;

public enum SensorDiagnosticStatus { Available, Unsupported, PermissionRequired, PermissionDenied, Initializing, Active, Inactive, Stale, Error }
public enum SensorPermission { NotRequired, Granted, Required, Denied, Unknown }
public sealed record DiagnosticsClient(string Id, ClientPlatform Platform, BuildInfo Build);
public sealed record DiagnosticSample(double Seconds, IReadOnlyList<SensorValue> Raw, IReadOnlyList<SensorValue> Filtered);
public sealed record SensorDiagnostic(SensorDescriptor Descriptor, string Description, SensorDiagnosticStatus Status,
    SensorPermission Permission, bool UsedByPositioning, string CalculationUsage, SensorReading? Latest,
    IReadOnlyList<SensorValue> Filtered, IReadOnlyList<DiagnosticSample> History, long Count, long RejectedCount,
    double? ActualFrequencyHz, double? RequestedFrequencyHz, TimeSpan? LastReceivedAge, string? Error);
public sealed record SensorDiagnosticsSnapshot(DiagnosticsClient? Client, IReadOnlyList<SensorDiagnostic> Sensors);

// Receiver contract for a trusted native adapter. Network transport/authentication belongs to iteration 07.
public interface ISensorDiagnosticsReceiver
{
    void SetClient(DiagnosticsClient client, IReadOnlyList<SensorDescriptor> catalogue);
    void SetState(SensorState state);
    void Receive(SensorReading reading);
    SensorDiagnosticsSnapshot GetSnapshot();
}

public sealed record SensorDiagnosticsOptions
{
    public TimeSpan StaleAfter { get; init; } = TimeSpan.FromSeconds(3);
    public TimeSpan FilterTimeConstant { get; init; } = TimeSpan.FromMilliseconds(250);
    public TimeSpan ChartInterval { get; init; } = TimeSpan.FromMilliseconds(100);
    public int HistoryCapacity { get; init; } = 180;
    public void Validate()
    {
        if (StaleAfter < TimeSpan.FromSeconds(1) || StaleAfter > TimeSpan.FromMinutes(1)) throw new ArgumentOutOfRangeException(nameof(StaleAfter));
        if (FilterTimeConstant < TimeSpan.FromMilliseconds(10) || FilterTimeConstant > TimeSpan.FromSeconds(10)) throw new ArgumentOutOfRangeException(nameof(FilterTimeConstant));
        if (ChartInterval < TimeSpan.FromMilliseconds(50) || ChartInterval > TimeSpan.FromSeconds(1)) throw new ArgumentOutOfRangeException(nameof(ChartInterval));
        if (HistoryCapacity is < 2 or > 600) throw new ArgumentOutOfRangeException(nameof(HistoryCapacity));
    }
}
