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
            vector.Select((value, index) => new SensorValue(new[] { "x", "y", "z" }[index], value, kind == SensorKind.StepCounter ? "count" : "m/s²")).ToArray(),
            (long)(time * 1e9), null, DateTimeOffset.UtcNow, SensorTimestampSource.AndroidElapsedRealtime, SensorQuality.Medium, "device"));
        public void Stop() => StateChanged?.Invoke(new(SensorKind.Accelerometer.ToString(), SensorStatus.Stopped));
        public Task<IReadOnlyList<SensorDescriptor>> DiscoverAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SensorDescriptor>>([]);
        public Task StartAsync(IReadOnlyCollection<string> sensorIds, SensorSamplingOptions options, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public IReadOnlyList<SensorStatistics> GetStatistics() => [];
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    [TestMethod]
    public void TestThat_session_configuration_preserves_counts_and_native_steps_are_not_added()
    {
        var provider = new Provider(); using var session = new StepSession(provider);
        var catalogue = new[] { SensorKind.Accelerometer, SensorKind.StepCounter }.Select(x => new SensorDescriptor(x.ToString(), "native", x.ToString(), "vendor", x, new(SensorReportingMode.Continuous), SensorStatus.Available)).ToArray();
        session.Configure(catalogue);
        for (var i = 0; i < 500; i++) { var wave = Math.Sin(2 * Math.PI * 2 * i * .02); provider.Emit(SensorKind.Accelerometer, i * .02, .6 * wave, 0, 9.80665 + 2 * wave); }
        var before = session.GetSnapshot(); Assert.IsGreaterThan(10L, before.Total);
        provider.Emit(SensorKind.StepCounter, 10, 999); Assert.AreEqual(before.Total, session.GetSnapshot().Total); Assert.AreEqual(999d, session.GetSnapshot().NativeTotal);
        session.Configure(catalogue); Assert.AreEqual(before.Total, session.GetSnapshot().Total);
        session.SetDeclaredForklift(true);
        for (var i = 500; i < 1000; i++) { var wave = Math.Sin(2 * Math.PI * 2 * i * .02); provider.Emit(SensorKind.Accelerometer, i * .02, .6 * wave, 0, 9.80665 + 2 * wave); }
        Assert.AreEqual(before.Total, session.GetSnapshot().Total); Assert.AreEqual(ActivityKind.Forklift, session.GetActivity().Kind);
        provider.Stop(); Assert.AreEqual(0d, session.GetSnapshot().CadenceHz);
        session.Reset(); Assert.AreEqual(0L, session.GetSnapshot().Total);
    }
}
