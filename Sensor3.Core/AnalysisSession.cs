using Sensor3.Contracts;
namespace Sensor3.Core;
// Input adapter for already acquired recordings or authenticated telemetry. It cannot acquire browser/native sensors.
public sealed class SensorInputStream(IReadOnlyList<SensorDescriptor> catalogue) : ISensorProvider
{
    public bool IsRunning => false;
    public event Action<SensorReading>? ReadingReceived;
    public event Action<SensorState>? StateChanged;
    public void Emit(SensorReading reading) => ReadingReceived?.Invoke(reading);
    public void Emit(SensorState state) => StateChanged?.Invoke(state);
    public Task<IReadOnlyList<SensorDescriptor>> DiscoverAsync(CancellationToken cancellationToken = default) => Task.FromResult(catalogue);
    public Task StartAsync(IReadOnlyCollection<string> sensorIds, SensorSamplingOptions options, CancellationToken cancellationToken = default) => throw new NotSupportedException("En inmatningsström kan inte samla native-sensorer.");
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public IReadOnlyList<SensorStatistics> GetStatistics() => [];
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
public sealed class AnalysisSession : IDisposable
{
    private readonly SensorInputStream stream;
    private readonly IReadOnlyList<SensorDescriptor> catalogue;
    private readonly Dictionary<SensorKind, SensorDescriptor> active = [];
    public StepSession Steps { get; }
    public NativeObservationBus Observations { get; } = new();
    public VehicleSession Vehicle { get; }
    public RadioPositionSession Radio { get; }
    public AnalysisSession(IReadOnlyList<SensorDescriptor> catalogue, TimeProvider? clock = null)
    {
        this.catalogue = catalogue; stream = new(catalogue); Steps = new(stream); Steps.Configure([]);
        Vehicle = new(Observations, Steps, Steps, clock); Radio = new(Observations, Steps, timeProvider: clock);
    }
    public void Receive(TelemetryEvent value)
    {
        if (value.Reading is { } reading)
        { if (!active.ContainsKey(reading.Kind)) { active[reading.Kind] = catalogue.First(x => x.Id == reading.SensorId); Steps.Configure(active.Values.ToArray()); } stream.Emit(reading); }
        else if (value.State is { } state) stream.Emit(state);
        else if (value.Location is { } fix) Observations.Publish(fix);
        else if (value.Wifi is { } wifi) Observations.Publish(wifi);
        else if (value.Bluetooth is { } ble) Observations.Publish(ble);
    }
    public void Dispose() { Radio.Dispose(); Vehicle.Dispose(); Steps.Dispose(); }
}
