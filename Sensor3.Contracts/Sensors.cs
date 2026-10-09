namespace Sensor3.Contracts;

public enum SensorKind { Accelerometer, Gyroscope, Gravity, LinearAcceleration, Magnetometer, RotationVector, Orientation, Barometer, StepDetector, StepCounter, Light, Proximity, Altimeter, Activity, HingeAngle, Other }
public enum SensorStatus { Unsupported, Available, PermissionRequired, Running, Stopped, Interrupted, Error }
public enum SensorQuality { Unknown, Unreliable, Low, Medium, High }
public enum SensorReportingMode { Continuous, OnChange, OneShot, SpecialTrigger }
public enum SensorTimestampSource { AndroidElapsedRealtime, WindowsUtc }
public sealed record SensorCapability(SensorReportingMode ReportingMode, double? MaximumFrequencyHz = null, bool RequiresPermission = false);
public sealed record SensorDescriptor(string Id, string NativeId, string Name, string Vendor, SensorKind Kind,
    SensorCapability Capability, SensorStatus Status, string? Detail = null);
public sealed record SensorValue(string Name, double Value, string Unit);
public sealed record SensorReading(string SensorId, SensorKind Kind, IReadOnlyList<SensorValue> Values,
    long? MonotonicTimestampNanoseconds, DateTimeOffset? TimestampUtc, DateTimeOffset ReceivedAtUtc,
    SensorTimestampSource TimestampSource, SensorQuality Quality, string CoordinateFrame);
public sealed record SensorState(string SensorId, SensorStatus Status, string? Detail = null);
public sealed record SensorStatistics(string SensorId, long ReceivedCount, long RejectedCount, double? ActualFrequencyHz, double RequestedFrequencyHz);
public sealed record SensorSamplingOptions
{
    public double FrequencyHz { get; init; } = 50;
    public TimeSpan InterruptionTimeout { get; init; } = TimeSpan.FromSeconds(3);
    public void Validate()
    {
        if (!double.IsFinite(FrequencyHz) || FrequencyHz is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(FrequencyHz), "Frekvens måste vara 1–200 Hz.");
        if (InterruptionTimeout < TimeSpan.FromSeconds(1) || InterruptionTimeout > TimeSpan.FromMinutes(1)) throw new ArgumentOutOfRangeException(nameof(InterruptionTimeout));
    }
}
public interface ISensorProvider : IAsyncDisposable
{
    bool IsRunning { get; }
    event Action<SensorReading>? ReadingReceived;
    event Action<SensorState>? StateChanged;
    Task<IReadOnlyList<SensorDescriptor>> DiscoverAsync(CancellationToken cancellationToken = default);
    Task StartAsync(IReadOnlyCollection<string> sensorIds, SensorSamplingOptions options, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    IReadOnlyList<SensorStatistics> GetStatistics();
}
public interface ISensorPermissionService
{
    Task RequestAsync(IReadOnlyCollection<SensorDescriptor> sensors, CancellationToken cancellationToken = default);
}
