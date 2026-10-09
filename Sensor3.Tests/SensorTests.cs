using Microsoft.Extensions.Logging.Abstractions;
using Sensor3.Contracts;
using Sensor3.Sensors;

namespace Sensor3.Tests;

[TestClass]
public sealed class SensorTests
{
    private static SensorDescriptor Descriptor(string id = "sensor", SensorReportingMode mode = SensorReportingMode.Continuous) =>
        new(id, "test native ID", "Synthetic unit-test sensor", "Test fixture", SensorKind.Accelerometer, new(mode), SensorStatus.Available);
    private static SensorReading Reading(long nanoseconds, string id = "sensor", double value = 1) =>
        new(id, SensorKind.Accelerometer, [new("x", value, "m/s²")], nanoseconds, null, DateTimeOffset.UtcNow, SensorTimestampSource.AndroidElapsedRealtime, SensorQuality.High, "Unit-test fixture");
    private sealed class Backend(params SensorDescriptor[] descriptors) : ISensorBackend
    {
        public int FailAt = -1;
        public int Subscribed;
        public int Disposed;
        public readonly Dictionary<string, Action<SensorReading>> Readers = [];
        public Task<IReadOnlyList<SensorDescriptor>> DiscoverAsync(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult<IReadOnlyList<SensorDescriptor>>(descriptors); }
        public Task<IAsyncDisposable> SubscribeAsync(SensorDescriptor sensor, SensorSamplingOptions options, Action<SensorReading> reading, Action<SensorState> state, CancellationToken cancellationToken)
        {
            if (++Subscribed == FailAt) throw new IOException("Synthetic connection failure");
            Readers[sensor.Id] = reading;
            return Task.FromResult<IAsyncDisposable>(new Subscription(() => Disposed++));
        }
        private sealed class Subscription(Action dispose) : IAsyncDisposable
        {
            private bool stopped;
            public ValueTask DisposeAsync() { if (!stopped) { stopped = true; dispose(); } return ValueTask.CompletedTask; }
        }
    }
    private sealed class Clock : TimeProvider
    {
        private long now;
        private readonly List<Timer> timers = [];
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => now;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        { var timer = new Timer(callback, state); timers.Add(timer); return timer; }
        public void Advance(TimeSpan time) { now += (long)time.TotalMilliseconds; foreach (var timer in timers.ToArray()) timer.Tick(); }
        private sealed class Timer(TimerCallback callback, object? state) : ITimer
        {
            private bool stopped;
            public bool Change(TimeSpan dueTime, TimeSpan period) => !stopped;
            public void Tick() { if (!stopped) callback(state); }
            public void Dispose() => stopped = true;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
    private static SensorProvider Provider(Backend backend, UpdateSessionGuard? guard = null, TimeProvider? clock = null) =>
        new(backend, guard ?? new(), NullLogger<SensorProvider>.Instance, clock);

    [TestMethod]
    public void TestThat_discovery_preserves_all_devices_and_marks_missing_kinds_unsupported()
    {
        var catalogue = SensorNormalization.CompleteCatalogue([Descriptor("first"), Descriptor("second")]);
        Assert.HasCount(2, catalogue.Where(x => x.Kind == SensorKind.Accelerometer));
        Assert.AreEqual(SensorStatus.Unsupported, catalogue.Single(x => x.Kind == SensorKind.Gyroscope).Status);
        Assert.IsTrue(catalogue.Where(x => x.Status == SensorStatus.Unsupported).All(x => x.NativeId == ""));
    }
    [TestMethod]
    public void TestThat_duplicate_device_ids_are_rejected() => Assert.ThrowsExactly<InvalidDataException>(() => SensorNormalization.CompleteCatalogue([Descriptor(), Descriptor()]));
    [TestMethod]
    [DataRow(2, 50f, 0.00005, "T", DisplayName = "Android mikrotesla till tesla")]
    [DataRow(6, 1013.25f, 101325.0, "Pa", DisplayName = "Android hPa till pascal")]
    [DataRow(8, 25f, 0.25, "m", DisplayName = "Android centimeter till meter")]
    [DataRow(3, 180f, Math.PI, "rad", DisplayName = "Android orientering till radianer")]
    [DataRow(4, 2f, 2.0, "rad/s", DisplayName = "Android gyro redan radianer per sekund")]
    [DataRow(1, 9.81f, 9.81, "m/s²", DisplayName = "Android acceleration redan SI")]
    [DataRow(36, 180f, Math.PI, "rad", DisplayName = "Android gångjärn till radianer")]
    [DataRow(13, 20f, 293.15, "K", DisplayName = "Android temperatur till kelvin")]
    [DataRow(12, 50f, 0.5, "1", DisplayName = "Android relativ fuktighet till andel")]
    [DataRow(21, 60f, 1.0, "Hz", DisplayName = "Android rå hjärtfrekvens till per sekund")]
    public void TestThat_android_units_are_normalized(int type, float native, double expected, string unit)
    {
        var value = SensorNormalization.AndroidValues(type, [native]).Single();
        Assert.AreEqual(expected, value.Value, 0.00001);
        Assert.AreEqual(unit, value.Unit);
    }
    [TestMethod]
    public void TestThat_windows_units_use_standard_gravity_and_radians()
    {
        Assert.AreEqual(9.80665, SensorNormalization.GToMetersPerSecondSquared(1), 1e-10);
        Assert.AreEqual(Math.PI, SensorNormalization.DegreesToRadians(180), 1e-10);
        Assert.AreEqual(101325, SensorNormalization.HectopascalToPascal(1013.25), 1e-10);
    }
    [TestMethod]
    public void TestThat_unknown_sensor_values_are_preserved_without_invented_units()
    {
        var values = SensorNormalization.AndroidValues(999, [1, 2, 3, 4, 5, 6, 7]);
        Assert.HasCount(7, values);
        Assert.IsTrue(values.All(x => x.Unit == "native"));
        Assert.AreEqual(7.0, values[6].Value);
    }
    [TestMethod]
    public void TestThat_rotation_heading_accuracy_keeps_its_own_unit()
    {
        var values = SensorNormalization.AndroidValues(11, [0, 0, 0, 1, .1f]);
        Assert.AreEqual("w", values[3].Name);
        Assert.AreEqual("1", values[3].Unit);
        Assert.AreEqual("rad", values[4].Unit);
    }
    [TestMethod]
    public void TestThat_platform_sampling_requests_respect_driver_minimum()
    {
        Assert.AreEqual(20000, SensorNormalization.AndroidSamplingPeriodMicroseconds(50));
        Assert.AreEqual(20u, SensorNormalization.WindowsReportIntervalMilliseconds(50, 10));
        Assert.AreEqual(100u, SensorNormalization.WindowsReportIntervalMilliseconds(50, 100));
    }
    [TestMethod]
    [DataRow(0.0, DisplayName = "Noll frekvens")]
    [DataRow(-1.0, DisplayName = "Negativ frekvens")]
    [DataRow(201.0, DisplayName = "Över säker gräns")]
    [DataRow(double.NaN, DisplayName = "NaN frekvens")]
    [DataRow(double.PositiveInfinity, DisplayName = "Oändlig frekvens")]
    public void TestThat_invalid_sampling_rate_is_rejected(double rate) => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new SensorSamplingOptions { FrequencyHz = rate }.Validate());
    [TestMethod]
    public async Task TestThat_measured_android_rate_uses_native_nanoseconds_and_not_requested_rate()
    {
        var backend = new Backend(Descriptor());
        await using var provider = Provider(backend);
        await provider.StartAsync(["sensor"], new() { FrequencyHz = 50 });
        backend.Readers["sensor"](Reading(1_000_000_000));
        Assert.IsNull(provider.GetStatistics().Single().ActualFrequencyHz);
        backend.Readers["sensor"](Reading(1_100_000_000));
        backend.Readers["sensor"](Reading(1_200_000_000));
        Assert.AreEqual(10.0, provider.GetStatistics().Single().ActualFrequencyHz);
        Assert.AreEqual(50.0, provider.GetStatistics().Single().RequestedFrequencyHz);
    }
    [TestMethod]
    public async Task TestThat_windows_utc_timestamps_do_not_overflow_and_are_preserved()
    {
        var backend = new Backend(Descriptor());
        await using var provider = Provider(backend);
        await provider.StartAsync(["sensor"], new());
        var timestamp = DateTimeOffset.Parse("2026-10-09T12:00:00Z");
        SensorReading? received = null;
        provider.ReadingReceived += x => received = x;
        backend.Readers["sensor"](Reading(0) with { MonotonicTimestampNanoseconds = null, TimestampUtc = timestamp, TimestampSource = SensorTimestampSource.WindowsUtc });
        backend.Readers["sensor"](Reading(0) with { MonotonicTimestampNanoseconds = null, TimestampUtc = timestamp.AddMilliseconds(20), TimestampSource = SensorTimestampSource.WindowsUtc });
        Assert.AreEqual(timestamp.AddMilliseconds(20), received!.TimestampUtc);
        Assert.AreEqual(50.0, provider.GetStatistics().Single().ActualFrequencyHz);
    }
    [TestMethod]
    public async Task TestThat_invalid_values_and_nonincreasing_timestamps_are_rejected()
    {
        var backend = new Backend(Descriptor());
        await using var provider = Provider(backend);
        await provider.StartAsync(["sensor"], new());
        backend.Readers["sensor"](Reading(100));
        backend.Readers["sensor"](Reading(100));
        backend.Readers["sensor"](Reading(90));
        backend.Readers["sensor"](Reading(200, value: double.NaN));
        backend.Readers["sensor"](Reading(200) with { Values = [] });
        Assert.AreEqual(1L, provider.GetStatistics().Single().ReceivedCount);
        Assert.AreEqual(4L, provider.GetStatistics().Single().RejectedCount);
    }
    [TestMethod]
    public async Task TestThat_stop_unregisters_and_rejects_late_callbacks_and_releases_update_guard()
    {
        var backend = new Backend(Descriptor()); var guard = new UpdateSessionGuard();
        await using var provider = Provider(backend, guard);
        await provider.StartAsync(["sensor"], new());
        Assert.ThrowsExactly<InvalidOperationException>(() => guard.AcquireInstallation());
        await provider.StopAsync();
        backend.Readers["sensor"](Reading(100));
        Assert.AreEqual(0L, provider.GetStatistics().Single().ReceivedCount);
        Assert.AreEqual(1, backend.Disposed);
        Assert.IsFalse(provider.IsRunning);
        using var lease = guard.AcquireInstallation();
    }
    [TestMethod]
    public async Task TestThat_partial_start_failure_unregisters_all_opened_devices()
    {
        var backend = new Backend(Descriptor("first"), Descriptor("second")) { FailAt = 2 }; var guard = new UpdateSessionGuard();
        await using var provider = Provider(backend, guard);
        await Assert.ThrowsExactlyAsync<IOException>(() => provider.StartAsync(["first", "second"], new()));
        Assert.AreEqual(1, backend.Disposed);
        Assert.IsFalse(provider.IsRunning);
        Assert.IsFalse(guard.IsSessionActive);
    }
    [TestMethod]
    public async Task TestThat_continuous_stream_interruption_and_recovery_are_reported()
    {
        var backend = new Backend(Descriptor()); var clock = new Clock();
        await using var provider = Provider(backend, clock: clock);
        var states = new List<SensorStatus>(); provider.StateChanged += x => states.Add(x.Status);
        await provider.StartAsync(["sensor"], new());
        clock.Advance(TimeSpan.FromSeconds(4));
        Assert.AreEqual(SensorStatus.Interrupted, states.Last());
        backend.Readers["sensor"](Reading(100));
        Assert.AreEqual(SensorStatus.Running, states.Last());
    }
    [TestMethod]
    [DataRow(SensorReportingMode.OnChange, DisplayName = "Ändringsstyrd sensor")]
    [DataRow(SensorReportingMode.OneShot, DisplayName = "Engångstrigger")]
    [DataRow(SensorReportingMode.SpecialTrigger, DisplayName = "Specialtrigger")]
    public async Task TestThat_event_based_sensors_are_not_marked_interrupted_while_idle(SensorReportingMode mode)
    {
        var backend = new Backend(Descriptor(mode: mode)); var clock = new Clock();
        await using var provider = Provider(backend, clock: clock);
        var states = new List<SensorStatus>(); provider.StateChanged += x => states.Add(x.Status);
        await provider.StartAsync(["sensor"], new());
        clock.Advance(TimeSpan.FromSeconds(20));
        Assert.AreEqual(SensorStatus.Running, states.Last());
    }
    [TestMethod]
    public async Task TestThat_unknown_and_permission_denied_devices_cannot_start()
    {
        await using var provider = Provider(new(Descriptor() with { Status = SensorStatus.PermissionRequired }));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => provider.StartAsync(["missing"], new()));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => provider.StartAsync(["sensor"], new()));
    }
    [TestMethod]
    public async Task TestThat_second_start_and_discovery_during_collection_are_rejected()
    {
        await using var provider = Provider(new(Descriptor()));
        await provider.StartAsync(["sensor"], new());
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => provider.StartAsync(["sensor"], new()));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => provider.DiscoverAsync());
    }
    [TestMethod]
    public async Task TestThat_restart_ignores_previous_session_callback()
    {
        var backend = new Backend(Descriptor());
        await using var provider = Provider(backend);
        await provider.StartAsync(["sensor"], new());
        var old = backend.Readers["sensor"];
        await provider.StopAsync();
        await provider.StartAsync(["sensor"], new());
        old(Reading(100));
        backend.Readers["sensor"](Reading(200));
        Assert.AreEqual(1L, provider.GetStatistics().Single().ReceivedCount);
    }
    [TestMethod]
    public async Task TestThat_observer_failure_does_not_break_other_consumers()
    {
        var backend = new Backend(Descriptor());
        await using var provider = Provider(backend);
        var received = false;
        provider.ReadingReceived += _ => throw new InvalidOperationException("Synthetic consumer failure");
        provider.ReadingReceived += _ => received = true;
        await provider.StartAsync(["sensor"], new());
        backend.Readers["sensor"](Reading(100));
        Assert.IsTrue(received);
    }
}
