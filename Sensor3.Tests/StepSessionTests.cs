using Sensor3.Contracts;
using Sensor3.Core;

namespace Sensor3.Tests;

[TestClass]
public sealed class StepSessionTests
{
    private sealed class Provider : ISensorProvider
    {
        public bool IsRunning => true;
        public event Action<SensorReading>? ReadingReceived;
        public event Action<SensorState>? StateChanged;
        public void Emit(SensorKind kind, double time, params double[] vector) => ReadingReceived?.Invoke(new(kind.ToString(), kind,
            vector.Select((value, index) => new SensorValue(new[] { "x", "y", "z" }[index], value, kind is SensorKind.StepCounter or SensorKind.StepDetector ? "count" : "m/s²")).ToArray(),
            (long)(time * 1e9), null, DateTimeOffset.UtcNow, SensorTimestampSource.AndroidElapsedRealtime, SensorQuality.Medium, "device"));
        public void Stop(SensorKind kind = SensorKind.Accelerometer) => StateChanged?.Invoke(new(kind.ToString(), SensorStatus.Stopped));
        public Task<IReadOnlyList<SensorDescriptor>> DiscoverAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SensorDescriptor>>([]);
        public Task StartAsync(IReadOnlyCollection<string> sensorIds, SensorSamplingOptions options, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public IReadOnlyList<SensorStatistics> GetStatistics() => [];
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private static SensorDescriptor[] Catalogue(params SensorKind[] kinds) => kinds.Select(x => new SensorDescriptor(x.ToString(), "native", x.ToString(), "vendor", x, new(SensorReportingMode.Continuous), SensorStatus.Available)).ToArray();
    [TestMethod]
    public void TestThat_imu_motion_is_not_counted_without_a_native_step_sensor()
    {
        var provider = new Provider(); using var session = new StepSession(provider);
        session.Configure(Catalogue(SensorKind.Accelerometer));
        for (var i = 0; i < 500; i++) { var wave = Math.Sin(2 * Math.PI * 2 * i * .02); provider.Emit(SensorKind.Accelerometer, i * .02, .6 * wave, 0, 9.80665 + 2 * wave); }
        Assert.AreEqual(0L, session.GetSnapshot().Total); Assert.AreEqual("Unknown", session.GetSnapshot().Source);
    }
    [TestMethod]
    public void TestThat_counter_baseline_duplicates_reset_and_source_configuration_preserve_session_counts()
    {
        var provider = new Provider(); using var session = new StepSession(provider);
        session.Configure(Catalogue(SensorKind.StepCounter));
        provider.Emit(SensorKind.StepCounter, 1, 999); Assert.AreEqual(0L, session.GetSnapshot().Total);
        provider.Emit(SensorKind.StepCounter, 2, 1001); Assert.AreEqual(2L, session.GetSnapshot().Total);
        provider.Emit(SensorKind.StepCounter, 2, 1005); provider.Emit(SensorKind.StepCounter, 1, 1008);
        Assert.AreEqual(2L, session.GetSnapshot().Total);
        session.Configure(Catalogue(SensorKind.StepCounter, SensorKind.Accelerometer));
        provider.Emit(SensorKind.StepCounter, 3, 1002); Assert.AreEqual(3L, session.GetSnapshot().Total);
        provider.Emit(SensorKind.StepCounter, 4, 2); provider.Emit(SensorKind.StepCounter, 5, 3);
        Assert.AreEqual(4L, session.GetSnapshot().Total);
        session.Reset(); provider.Emit(SensorKind.StepCounter, 6, 4); provider.Emit(SensorKind.StepCounter, 7, 5);
        Assert.AreEqual(1L, session.GetSnapshot().Total); Assert.AreEqual("Native StepCounter", session.GetSnapshot().Source);
    }
    [TestMethod]
    public void TestThat_detector_events_are_not_double_counted_with_counter_or_invalid_events()
    {
        var provider = new Provider(); using var session = new StepSession(provider);
        session.Configure(Catalogue(SensorKind.StepDetector, SensorKind.StepCounter));
        provider.Emit(SensorKind.StepDetector, 1, 1); provider.Emit(SensorKind.StepDetector, 1, 1);
        provider.Emit(SensorKind.StepCounter, 2, 100); provider.Emit(SensorKind.StepCounter, 3, 110);
        provider.Emit(SensorKind.StepDetector, 4, 0); provider.Emit(SensorKind.StepDetector, 5, 2);
        provider.Emit(SensorKind.StepDetector, 6, double.NaN); provider.Emit(SensorKind.StepDetector, 7, 1);
        Assert.AreEqual(2L, session.GetSnapshot().Total); Assert.AreEqual(0L, session.GetSnapshot().Walking);
        Assert.AreEqual(110d, session.GetSnapshot().NativeTotal); Assert.AreEqual("Native StepDetector", session.GetSnapshot().Source);
    }
    [TestMethod]
    public void TestThat_resume_does_not_count_background_steps()
    {
        var provider = new Provider(); using var session = new StepSession(provider);
        var catalogue = Catalogue(SensorKind.StepCounter); session.Configure(catalogue);
        provider.Emit(SensorKind.StepCounter, 1, 10); provider.Emit(SensorKind.StepCounter, 2, 12);
        provider.Stop(SensorKind.StepCounter); provider.Emit(SensorKind.StepCounter, 3, 20);
        Assert.AreEqual(2L, session.GetSnapshot().Total);
        session.Configure(catalogue); provider.Emit(SensorKind.StepCounter, 4, 30); provider.Emit(SensorKind.StepCounter, 5, 31);
        Assert.AreEqual(3L, session.GetSnapshot().Total);
    }
    [TestMethod]
    public void TestThat_declared_truck_advances_counter_baseline_without_counting_steps()
    {
        var provider = new Provider(); using var session = new StepSession(provider);
        session.Configure(Catalogue(SensorKind.StepCounter)); provider.Emit(SensorKind.StepCounter, 1, 10);
        session.SetDeclaredForklift(true); provider.Emit(SensorKind.StepCounter, 2, 20);
        session.SetDeclaredForklift(false); provider.Emit(SensorKind.StepCounter, 3, 21);
        Assert.AreEqual(1L, session.GetSnapshot().Total);
    }
    [TestMethod]
    public void TestThat_implausible_counter_jump_is_rebased_without_overflow()
    {
        var provider = new Provider(); using var session = new StepSession(provider);
        session.Configure(Catalogue(SensorKind.StepCounter)); provider.Emit(SensorKind.StepCounter, 1, 0);
        provider.Emit(SensorKind.StepCounter, 2, 1_000_000); provider.Emit(SensorKind.StepCounter, 3, 1_000_001);
        Assert.AreEqual(1L, session.GetSnapshot().Total);
    }
    [TestMethod]
    public void TestThat_native_counter_batch_does_not_invent_individual_step_positions()
    {
        var position = new Sensor3.Positioning.PedestrianPositionEstimator(); position.SetStart(0, 0);
        position.AddNative(3, 1, 1, .7, false, new(0, .9, .1, "test", ""), .9);
        Assert.AreEqual(new LocalPoint(0, 0), position.Snapshot().Position);
        Assert.AreEqual(2.1, position.Snapshot().WalkingMeters, 1e-8);
        Assert.IsNull(position.Snapshot().SpeedMetersPerSecond);
        position.AddNative(1, 2, 1, .7, false, new(0, .9, .1, "test", ""), .9);
        Assert.AreEqual(.7, position.Snapshot().Position!.Y, 1e-8);
    }
}