using Sensor3.Contracts;
using Sensor3.Infrastructure;

namespace Sensor3.Tests;

[TestClass]
public sealed class TelemetryTests
{
    private static TelemetryRegistration Registration(string device, Guid? session = null) => new(new(device, ClientPlatform.Android, new() { ApplicationVersion = "native-test" }), session ?? Guid.NewGuid(), TelemetryMode.Research,
        [new("accel", "native", "Accelerometer", "vendor", SensorKind.Accelerometer, new(SensorReportingMode.Continuous), SensorStatus.Available)],
        Enumerable.Range(1, 18).Select(x => new IterationInfo(x, "test", "test", IterationStatus.NotStarted, "Unknown", null, null, "NotRun", [])).ToArray());
    private static TelemetryBatch Batch(Guid session, long sequence, double value = 1) => new(session, sequence, DateTimeOffset.UnixEpoch,
        [new(new("accel", SensorKind.Accelerometer, [new("x", value, "m/s²")], (sequence + 1) * 10000000, null, DateTimeOffset.UtcNow, SensorTimestampSource.AndroidElapsedRealtime, SensorQuality.Medium, "device"))]);
    [TestMethod]
    public void TestThat_retry_is_deduplicated_and_conflicting_packet_is_rejected()
    {
        var registry = new TelemetryRegistry(); var registration = Registration("one"); registry.Register("one", registration);
        var batch = Batch(registration.SessionId, 0);
        Assert.IsFalse(registry.Apply("one", batch)); Assert.IsTrue(registry.Apply("one", batch));
        Assert.AreEqual(1L, registry.GetDiagnostics(registration.SessionId).Sensors.Single().Count);
        Assert.ThrowsExactly<InvalidDataException>(() => registry.Apply("one", batch with { Events = Batch(registration.SessionId, 0, 2).Events }));
    }
    [TestMethod]
    public void TestThat_sessions_cannot_be_accessed_by_other_devices()
    {
        var registry = new TelemetryRegistry(); var registration = Registration("one"); registry.Register("one", registration);
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => registry.Apply("two", Batch(registration.SessionId, 0)));
        Assert.ThrowsExactly<InvalidDataException>(() => registry.Register("two", registration));
    }
    [TestMethod]
    public void TestThat_gap_is_counted_and_old_packet_does_not_replace_current_data()
    {
        var registry = new TelemetryRegistry(); var registration = Registration("one"); registry.Register("one", registration);
        registry.Apply("one", Batch(registration.SessionId, 2)); registry.Apply("one", Batch(registration.SessionId, 1, 99));
        Assert.AreEqual(2L, registry.GetSessions().Single().Statistics.MissingPackets);
        Assert.AreEqual(1d, registry.GetDiagnostics(registration.SessionId).Sensors.Single().Latest!.Values.Single().Value);
    }
    [TestMethod]
    public void TestThat_delayed_disconnect_does_not_interrupt_reconnected_session()
    {
        var registry = new TelemetryRegistry(); var registration = Registration("one"); registry.Register("one", registration, "old");
        registry.Register("one", registration, "new"); registry.Disconnect("one", registration.SessionId, "old");
        Assert.AreEqual("Connected", registry.GetSessions().Single().Statistics.Status);
        registry.Disconnect("one", registration.SessionId, "new"); Assert.AreEqual("Disconnected", registry.GetSessions().Single().Statistics.Status);
    }
    [TestMethod]
    public async Task TestThat_parallel_clients_preserve_all_samples_and_native_timestamps()
    {
        var registry = new TelemetryRegistry(); var registrations = Enumerable.Range(0, 8).Select(x => Registration($"device{x}")).ToArray();
        foreach (var registration in registrations) registry.Register(registration.Client.Id, registration);
        await Task.WhenAll(registrations.Select(registration => Task.Run(() => { for (var i = 0; i < 100; i++) registry.Apply(registration.Client.Id, Batch(registration.SessionId, i)); })));
        foreach (var registration in registrations)
        {
            var sensor = registry.GetDiagnostics(registration.SessionId).Sensors.Single(); Assert.AreEqual(100L, sensor.Count);
            Assert.AreEqual(1000000000L, sensor.Latest!.MonotonicTimestampNanoseconds);
        }
    }
    [TestMethod]
    public void TestThat_idle_heartbeat_does_not_create_sensor_measurements()
    {
        var registry = new TelemetryRegistry(); var registration = Registration("one"); registry.Register("one", registration);
        registry.Apply("one", new(registration.SessionId, 0, DateTimeOffset.UtcNow, []));
        Assert.AreEqual(0L, registry.GetDiagnostics(registration.SessionId).Sensors.Single().Count);
    }
    [TestMethod]
    public async Task TestThat_full_server_queue_applies_cancellable_backpressure()
    {
        var queue = new TelemetryQueue(); using var cancellation = new CancellationTokenSource();
        var pending = Enumerable.Range(0, 65).Select(x => queue.SubmitAsync("one", Batch(Guid.NewGuid(), x), cancellation.Token)).ToArray();
        cancellation.Cancel();
        foreach (var task in pending.Take(64)) await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () => await task);
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await pending[64]);
    }
}
