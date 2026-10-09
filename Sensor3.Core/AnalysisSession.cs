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
        else if (value.Context is { } context) Configure(context);
        else if (value.Location is { } fix) Observations.Publish(fix);
        else if (value.Wifi is { } wifi) Observations.Publish(wifi);
        else if (value.Bluetooth is { } ble) Observations.Publish(ble);
    }
    public void Configure(AnalysisContext context)
    {
        SensorEventValidation.ValidateContext(context);
        if (context.ResetCounters) Steps.Reset();
        if (context.DeclaredForklift is { } truck) Steps.SetDeclaredForklift(truck);
        if (context.DeclaredCarrying is { } carry) Steps.SetDeclaredCarrying(carry);
        if (context.KnownStartHeading is { } heading) Steps.SetKnownStartHeading(heading);
        if (context.StartPosition is { } p) Steps.SetStartPosition(p.X, p.Y);
        if (context.GpsOrigin is { } gps) Vehicle.SetGpsOrigin(gps.Y, gps.X);
    }
    public RemoteAnalysisSnapshot Snapshot() => new(Steps.GetSnapshot(), Steps.GetActivity(), Steps.GetCarrying(), Steps.GetHeading(), Steps.GetFusionSnapshot(), Steps.GetPosition(), Vehicle.GetSnapshot(), Radio.GetRadioPosition());
    public void Interrupt()
    {
        foreach (var descriptor in active.Values.ToArray()) stream.Emit(new SensorState(descriptor.Id, SensorStatus.Interrupted, "Källström avbruten."));
    }
    public void Dispose() { Radio.Dispose(); Vehicle.Dispose(); Steps.Dispose(); }
}
