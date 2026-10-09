using Sensor3.Contracts;
using Sensor3.Sensors;

namespace Sensor3.Tests;

[TestClass]
public sealed class SensorDiagnosticsTests
{
    private sealed class Clock : TimeProvider
    {
        private long timestamp;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => timestamp;
        public void Advance(double seconds) => timestamp += (long)(seconds * 1000);
    }
    private static SensorDescriptor Descriptor(SensorStatus status = SensorStatus.Available, SensorReportingMode mode = SensorReportingMode.Continuous, SensorKind kind = SensorKind.Accelerometer) =>
        new("sensor", "native", "Actual native descriptor", "Vendor", kind, new(mode, 100, true), status);
    private static SensorReading Reading(double seconds, double value = 1, SensorKind kind = SensorKind.Accelerometer, string unit = "m/s²") =>
        new("sensor", kind, [new("x", value, unit)], (long)(seconds * 1e9), null, DateTimeOffset.UtcNow, SensorTimestampSource.AndroidElapsedRealtime, SensorQuality.Medium, "native");
    private static SensorDiagnosticsStore Store(SensorDescriptor? descriptor = null, Clock? clock = null, SensorDiagnosticsOptions? options = null)
    {
        var store = new SensorDiagnosticsStore(options, clock);
        store.SetClient(new("native-client", ClientPlatform.Android, new() { ApplicationVersion = "client-version", LatestImplementedIteration = 5, GitCommitHash = "client-commit" }), [descriptor ?? Descriptor()]);
        return store;
    }
    [TestMethod]
    [DataRow(SensorStatus.Available, SensorDiagnosticStatus.Available, DisplayName = "Available")]
    [DataRow(SensorStatus.Unsupported, SensorDiagnosticStatus.Unsupported, DisplayName = "Unsupported")]
    [DataRow(SensorStatus.PermissionRequired, SensorDiagnosticStatus.PermissionRequired, DisplayName = "PermissionRequired")]
    [DataRow(SensorStatus.PermissionDenied, SensorDiagnosticStatus.PermissionDenied, DisplayName = "PermissionDenied")]
    [DataRow(SensorStatus.Running, SensorDiagnosticStatus.Active, DisplayName = "Active")]
    [DataRow(SensorStatus.Stopped, SensorDiagnosticStatus.Inactive, DisplayName = "Inactive")]
    [DataRow(SensorStatus.Interrupted, SensorDiagnosticStatus.Stale, DisplayName = "Stale")]
    [DataRow(SensorStatus.Error, SensorDiagnosticStatus.Error, DisplayName = "Error")]
    public void TestThat_native_states_map_to_distinct_diagnostic_states(SensorStatus native, SensorDiagnosticStatus expected) =>
        Assert.AreEqual(expected, SensorDiagnosticsStore.MapStatus(native));

    [TestMethod]
    public void TestThat_initializing_requires_actual_data_and_times_out()
    {
        var clock = new Clock(); var store = Store(clock: clock);
        store.BeginSession(["sensor"], 50);
        store.SetState(new("sensor", SensorStatus.Running));
        Assert.AreEqual(SensorDiagnosticStatus.Initializing, store.GetSnapshot().Sensors.Single().Status);
        clock.Advance(3);
        Assert.AreEqual(SensorDiagnosticStatus.Stale, store.GetSnapshot().Sensors.Single().Status);
        store.Receive(Reading(10));
        Assert.AreEqual(SensorDiagnosticStatus.Active, store.GetSnapshot().Sensors.Single().Status);
        clock.Advance(3);
        Assert.AreEqual(SensorDiagnosticStatus.Stale, store.GetSnapshot().Sensors.Single().Status);
        store.Receive(Reading(11));
        Assert.AreEqual(SensorDiagnosticStatus.Active, store.GetSnapshot().Sensors.Single().Status);
    }
    [TestMethod]
    [DataRow(SensorReportingMode.OnChange, DisplayName = "OnChange")]
    [DataRow(SensorReportingMode.OneShot, DisplayName = "OneShot")]
    [DataRow(SensorReportingMode.SpecialTrigger, DisplayName = "SpecialTrigger")]
    public void TestThat_event_sensors_do_not_become_stale_from_silence(SensorReportingMode mode)
    {
        var clock = new Clock(); var store = Store(Descriptor(mode: mode), clock);
        store.BeginSession(["sensor"], 50); store.Receive(Reading(10)); clock.Advance(60);
        Assert.AreEqual(SensorDiagnosticStatus.Active, store.GetSnapshot().Sensors.Single().Status);
    }
    [TestMethod]
    public void TestThat_stop_retains_last_data_without_turning_stale_and_ignores_late_data()
    {
        var clock = new Clock(); var store = Store(clock: clock);
        store.BeginSession(["sensor"], 50); store.Receive(Reading(10));
        store.SetState(new("sensor", SensorStatus.Stopped)); clock.Advance(60); store.Receive(Reading(11));
        var sensor = store.GetSnapshot().Sensors.Single();
        Assert.AreEqual(SensorDiagnosticStatus.Inactive, sensor.Status);
        Assert.AreEqual(1L, sensor.Count);
        Assert.IsNotNull(sensor.Latest);
    }
    [TestMethod]
    public void TestThat_denial_and_error_are_explicit_and_do_not_invent_measurements()
    {
        var store = Store(Descriptor(SensorStatus.PermissionDenied)); store.Receive(Reading(1));
        var denied = store.GetSnapshot().Sensors.Single();
        Assert.AreEqual(SensorPermission.Denied, denied.Permission); Assert.IsNull(denied.Latest);
        store.SetState(new("sensor", SensorStatus.Error, "Native failure"));
        Assert.AreEqual("Native failure", store.GetSnapshot().Sensors.Single().Error);
    }
    [TestMethod]
    public void TestThat_filter_uses_native_time_and_preserves_raw_values()
    {
        var store = Store(); store.BeginSession(["sensor"], 50);
        store.Receive(Reading(10, 0)); store.Receive(Reading(10.25, 10));
        var sensor = store.GetSnapshot().Sensors.Single();
        Assert.AreEqual(10.0, sensor.Latest!.Values.Single().Value);
        Assert.AreEqual(10 * (1 - Math.Exp(-1)), sensor.Filtered.Single().Value, 1e-9);
        Assert.AreEqual(4.0, sensor.ActualFrequencyHz!.Value, 1e-9);
        Assert.IsFalse(sensor.UsedByPositioning);
    }
    [TestMethod]
    public void TestThat_filter_resets_after_native_gap_and_on_session_restart()
    {
        var store = Store(); store.BeginSession(["sensor"], 50);
        store.Receive(Reading(10, 0)); store.Receive(Reading(20, 10));
        Assert.AreEqual(10.0, store.GetSnapshot().Sensors.Single().Filtered.Single().Value);
        store.BeginSession(["sensor"], 50); store.Receive(Reading(21, 20));
        Assert.AreEqual(20.0, store.GetSnapshot().Sensors.Single().Filtered.Single().Value);
        Assert.AreEqual(1L, store.GetSnapshot().Sensors.Single().Count);
    }
    [TestMethod]
    public void TestThat_quaternions_categories_and_unknown_units_are_not_filtered()
    {
        var store = Store(Descriptor(kind: SensorKind.RotationVector));
        store.BeginSession(["sensor"], 50); store.Receive(Reading(10, kind: SensorKind.RotationVector, unit: "1"));
        Assert.IsEmpty(store.GetSnapshot().Sensors.Single().Filtered);
        store = Store(); store.BeginSession(["sensor"], 50); store.Receive(Reading(10, unit: "native"));
        Assert.IsEmpty(store.GetSnapshot().Sensors.Single().Filtered);
    }
    [TestMethod]
    public void TestThat_history_is_bounded_and_decimated_without_losing_sample_count()
    {
        var store = Store(options: new() { HistoryCapacity = 2 }); store.BeginSession(["sensor"], 200);
        for (var i = 0; i < 100; i++) store.Receive(Reading(10 + i * .005));
        var sensor = store.GetSnapshot().Sensors.Single();
        Assert.AreEqual(100L, sensor.Count); Assert.HasCount(2, sensor.History);
        Assert.AreEqual(200.0, sensor.ActualFrequencyHz!.Value, 1e-6);
    }
    [TestMethod]
    public void TestThat_invalid_and_non_increasing_data_cannot_make_sensor_active()
    {
        var store = Store(); store.BeginSession(["sensor"], 50);
        store.Receive(Reading(1, double.NaN));
        Assert.AreEqual(SensorDiagnosticStatus.Initializing, store.GetSnapshot().Sensors.Single().Status);
        store.Receive(Reading(1)); store.Receive(Reading(1)); store.Receive(Reading(.5));
        store.Receive(Reading(2) with { TimestampSource = SensorTimestampSource.WindowsUtc, TimestampUtc = DateTimeOffset.UtcNow });
        var sensor = store.GetSnapshot().Sensors.Single();
        Assert.AreEqual(1L, sensor.Count); Assert.AreEqual(4L, sensor.RejectedCount);
    }
    [TestMethod]
    public void TestThat_snapshots_and_input_arrays_cannot_mutate_store_history()
    {
        var store = Store(); store.BeginSession(["sensor"], 50);
        var values = new[] { new SensorValue("x", 1, "m/s²") }; store.Receive(Reading(1) with { Values = values });
        values[0] = values[0] with { Value = 99 };
        var snapshot = store.GetSnapshot(); ((SensorValue[])snapshot.Sensors.Single().Latest!.Values)[0] = values[0];
        Assert.AreEqual(1.0, store.GetSnapshot().Sensors.Single().Latest!.Values.Single().Value);
    }
    [TestMethod]
    public void TestThat_empty_receiver_has_unknown_client_and_uses_received_client_metadata()
    {
        Assert.IsNull(new SensorDiagnosticsStore().GetSnapshot().Client);
        var store = Store();
        Assert.AreEqual("client-version", store.GetSnapshot().Client!.Build.ApplicationVersion);
        Assert.AreEqual("client-commit", store.GetSnapshot().Client!.Build.GitCommitHash);
        Assert.IsNull(store.GetSnapshot().Sensors.Single().Latest);
    }
    [TestMethod]
    public void TestThat_windows_receiver_preserves_native_utc_and_frequency()
    {
        var store = new SensorDiagnosticsStore();
        store.SetClient(new("windows-client", ClientPlatform.Windows, new() { ApplicationVersion = "windows-version" }), [Descriptor()]);
        store.BeginSession(["sensor"], 50);
        var utc = new DateTimeOffset(2026, 10, 9, 14, 0, 0, TimeSpan.Zero);
        store.Receive(Reading(1) with { MonotonicTimestampNanoseconds = null, TimestampUtc = utc, TimestampSource = SensorTimestampSource.WindowsUtc });
        store.Receive(Reading(2) with { MonotonicTimestampNanoseconds = null, TimestampUtc = utc.AddMilliseconds(20), TimestampSource = SensorTimestampSource.WindowsUtc });
        var sensor = store.GetSnapshot().Sensors.Single();
        Assert.AreEqual(utc.AddMilliseconds(20), sensor.Latest!.TimestampUtc);
        Assert.AreEqual(50.0, sensor.ActualFrequencyHz!.Value, .001);
        Assert.AreEqual("windows-version", store.GetSnapshot().Client!.Build.ApplicationVersion);
    }
    [TestMethod]
    public void TestThat_invalid_receiver_identity_catalogue_and_configuration_are_rejected()
    {
        var store = new SensorDiagnosticsStore();
        Assert.ThrowsExactly<ArgumentException>(() => store.SetClient(new("", ClientPlatform.Windows, new()), []));
        Assert.ThrowsExactly<ArgumentException>(() => store.SetClient(new("client", (ClientPlatform)42, new()), []));
        Assert.ThrowsExactly<ArgumentException>(() => store.SetClient(new("client", ClientPlatform.Android, new()), [Descriptor(), Descriptor()]));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new SensorDiagnosticsStore(new() { HistoryCapacity = 1 }));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new SensorDiagnosticsStore(new() { StaleAfter = TimeSpan.Zero }));
    }
}
