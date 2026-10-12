using Microsoft.Extensions.Logging.Abstractions;
using Sensor3.Contracts;
using Sensor3.Core;
using Sensor3.Sensors;

namespace Sensor3.Tests;
[TestClass]
public sealed class PositioningSessionTests
{
    private sealed class Provider(List<string> calls) : ISensorProvider
    {
        public bool IsRunning { get; private set; }
        public string[] Selected { get; private set; } = [];
        public IReadOnlyList<SensorDescriptor> Catalogue { get; set; } = [Descriptor("accel", SensorStatus.Available), Descriptor("denied", SensorStatus.PermissionDenied), Descriptor("missing", SensorStatus.Unsupported)];
        public event Action<SensorReading>? ReadingReceived;
        public event Action<SensorState>? StateChanged;
        public Task<IReadOnlyList<SensorDescriptor>> DiscoverAsync(CancellationToken cancellationToken = default) => Task.FromResult(Catalogue);
        public Task StartAsync(IReadOnlyCollection<string> sensorIds, SensorSamplingOptions options, CancellationToken cancellationToken = default)
        {
            calls.Add("sensors"); Selected = sensorIds.ToArray(); IsRunning = true;
            StateChanged?.Invoke(new("accel", SensorStatus.Running));
            ReadingReceived?.Invoke(new("accel", SensorKind.Accelerometer, [new("x", 1, "m/s²"), new("y", 0, "m/s²"), new("z", 9.8, "m/s²")], 1000000000, null, DateTimeOffset.UtcNow, SensorTimestampSource.AndroidElapsedRealtime, SensorQuality.Medium, "device"));
            return Task.CompletedTask;
        }
        public Task StopAsync(CancellationToken cancellationToken = default) { calls.Add("stop-sensors"); IsRunning = false; return Task.CompletedTask; }
        public IReadOnlyList<SensorStatistics> GetStatistics() => [];
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private static SensorDescriptor Descriptor(string id, SensorStatus status) => new(id, id, id, "test", SensorKind.Accelerometer, new(SensorReportingMode.Continuous), status);
    private sealed class Recorder(List<string> calls) : IRecordingSession
    {
        public bool IsRecording { get; private set; }
        public int FrameCount => 0;
        public Task StartAsync(string name, CancellationToken cancellationToken = default) { calls.Add("recording"); IsRecording = true; return Task.CompletedTask; }
        public SensorRecording Stop() { calls.Add("stop-recording"); IsRecording = false; return new(1, "test", false, DateTimeOffset.UtcNow, [], []); }
    }
    private sealed class Telemetry(List<string> calls, bool fail, bool connected = false) : ITelemetryClient
    {
        public string DeviceId => "test-device";
        public BuildInfo? ServerBuild => null;
        public TelemetryStatistics GetStatistics() => new(0, 0, 0, 0, 0, 0, null, null, connected ? "Connected" : "Disconnected");
        public Task ConnectAsync(Uri endpoint, string token, TelemetryMode mode, CancellationToken cancellationToken = default)
        { calls.Add("connect"); if (fail) throw new InvalidOperationException("Server unavailable"); return Task.CompletedTask; }
        public Task DisconnectAsync(CancellationToken cancellationToken = default) { calls.Add("disconnect"); return Task.CompletedTask; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    [TestMethod]
    public async Task TestThat_one_start_connects_and_records_before_available_sensors_produce_data()
    {
        var calls = new List<string>(); var provider = new Provider(calls); var store = new SensorDiagnosticsStore();
        using var steps = new StepSession(provider); var recorder = new Recorder(calls);
        await using var session = new PositioningSession(provider, steps, store, recorder, new Telemetry(calls, false), new() { TargetPlatform = "net10.0-android" }, NullLogger<PositioningSession>.Instance);
        Assert.AreEqual(1, await session.StartAsync(new("https://example.test"), "token", TelemetryMode.Research));
        CollectionAssert.AreEqual(new[] { "disconnect", "connect", "recording", "sensors" }, calls);
        CollectionAssert.AreEqual(new[] { "accel" }, provider.Selected);
        Assert.AreEqual(1L, store.GetSnapshot().Sensors.Single(x => x.Descriptor.Id == "accel").Count);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => session.StartAsync(new("https://example.test"), "token", TelemetryMode.Research));
        await session.StopAsync(); Assert.IsFalse(provider.IsRunning); Assert.IsFalse(recorder.IsRecording); Assert.IsNotNull(session.LastRecording);
        CollectionAssert.AreEqual(new[] { "stop-sensors", "stop-recording", "disconnect" }, calls.TakeLast(3).ToArray());
    }
    [TestMethod]
    public async Task TestThat_server_failure_never_starts_sensors_or_recording()
    {
        var calls = new List<string>(); var provider = new Provider(calls); using var steps = new StepSession(provider); var recorder = new Recorder(calls);
        await using var session = new PositioningSession(provider, steps, new(), recorder, new Telemetry(calls, true), new() { TargetPlatform = "net10.0-android" }, NullLogger<PositioningSession>.Instance);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => session.StartAsync(new("https://example.test"), "token", TelemetryMode.Research));
        Assert.IsFalse(session.IsRunning); Assert.IsFalse(recorder.IsRecording); Assert.DoesNotContain("sensors", calls);
    }
    [TestMethod]
    public async Task TestThat_no_available_sensors_does_not_connect_or_start_a_log()
    {
        var calls = new List<string>(); var provider = new Provider(calls) { Catalogue = [Descriptor("denied", SensorStatus.PermissionDenied)] }; using var steps = new StepSession(provider);
        await using var session = new PositioningSession(provider, steps, new(), new Recorder(calls), new Telemetry(calls, false), new() { TargetPlatform = "net10.0-android" }, NullLogger<PositioningSession>.Instance);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => session.StartAsync(new("https://example.test"), "token", TelemetryMode.Research));
        Assert.IsEmpty(calls);
    }
    private sealed class Permissions(Provider provider) : ISensorPermissionService
    {
        public int Requests { get; private set; }
        public Task RequestAsync(IReadOnlyCollection<SensorDescriptor> sensors, CancellationToken cancellationToken = default)
        {
            Requests++; provider.Catalogue = provider.Catalogue.Select(x => sensors.Any(s => s.Id == x.Id) ? x with { Status = SensorStatus.Available } : x).ToArray();
            return Task.CompletedTask;
        }
    }
    [TestMethod]
    public async Task TestThat_required_permissions_are_requested_before_available_sensors_are_selected()
    {
        var calls = new List<string>(); var provider = new Provider(calls) { Catalogue = [Descriptor("accel", SensorStatus.PermissionRequired), Descriptor("denied", SensorStatus.PermissionDenied)] };
        using var steps = new StepSession(provider); var permissions = new Permissions(provider);
        await using var session = new PositioningSession(provider, steps, new(), new Recorder(calls), new Telemetry(calls, false), new() { TargetPlatform = "net10.0-android" }, NullLogger<PositioningSession>.Instance, permissions);
        Assert.AreEqual(1, await session.StartAsync(new("https://example.test"), "token", TelemetryMode.Research));
        Assert.AreEqual(1, permissions.Requests); CollectionAssert.AreEqual(new[] { "accel" }, provider.Selected);
    }
    [TestMethod]
    public async Task TestThat_existing_server_connection_is_reused_after_token_is_cleared()
    {
        var calls = new List<string>(); var provider = new Provider(calls); using var steps = new StepSession(provider);
        await using var session = new PositioningSession(provider, steps, new(), new Recorder(calls), new Telemetry(calls, false, true), new() { TargetPlatform = "net10.0-android" }, NullLogger<PositioningSession>.Instance);
        Assert.AreEqual(1, await session.StartAsync(new("https://example.test"), "", TelemetryMode.Research));
        CollectionAssert.AreEqual(new[] { "recording", "sensors" }, calls);
    }
}