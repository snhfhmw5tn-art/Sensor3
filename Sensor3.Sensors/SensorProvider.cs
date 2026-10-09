using Microsoft.Extensions.Logging;
using Sensor3.Contracts;

namespace Sensor3.Sensors;

public interface ISensorBackend
{
    Task<IReadOnlyList<SensorDescriptor>> DiscoverAsync(CancellationToken cancellationToken);
    Task<IAsyncDisposable> SubscribeAsync(SensorDescriptor sensor, SensorSamplingOptions options, Action<SensorReading> reading,
        Action<SensorState> state, CancellationToken cancellationToken);
}

public sealed class SensorProvider(ISensorBackend backend, UpdateSessionGuard sessionGuard, ILogger<SensorProvider> logger,
    TimeProvider? timeProvider = null) : ISensorProvider
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly SemaphoreSlim lifecycle = new(1, 1);
    private readonly object gate = new();
    private readonly List<IAsyncDisposable> subscriptions = [];
    private readonly Dictionary<string, StreamState> streams = [];
    private IDisposable? session;
    private ITimer? timer;
    private long generation;
    private bool disposed;
    private bool running;
    public bool IsRunning { get { lock (gate) return running; } }
    public event Action<SensorReading>? ReadingReceived;
    public event Action<SensorState>? StateChanged;

    private sealed class StreamState(SensorDescriptor descriptor, SensorSamplingOptions options, long started)
    {
        public readonly SensorDescriptor Descriptor = descriptor;
        public readonly SensorSamplingOptions Options = options;
        public long LastArrival = started;
        public long Count;
        public long Rejected;
        public long? FirstTimestamp;
        public long? LastTimestamp;
        public bool Interrupted;
        public SensorTimestampSource? TimestampSource;
    }
    public async Task<IReadOnlyList<SensorDescriptor>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        await lifecycle.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (IsRunning) throw new InvalidOperationException("Stoppa insamlingen före ny sensorinventering.");
            return SensorNormalization.CompleteCatalogue(await backend.DiscoverAsync(cancellationToken));
        }
        finally { lifecycle.Release(); }
    }
    public async Task StartAsync(IReadOnlyCollection<string> sensorIds, SensorSamplingOptions options, CancellationToken cancellationToken = default)
    {
        options.Validate();
        await lifecycle.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (IsRunning) throw new InvalidOperationException("En sensorsession pågår redan.");
            if (sensorIds.Count == 0 || sensorIds.Distinct(StringComparer.Ordinal).Count() != sensorIds.Count) throw new ArgumentException("Välj minst en sensor utan duplicerade ID:n.");
            var catalogue = SensorNormalization.CompleteCatalogue(await backend.DiscoverAsync(cancellationToken));
            var selected = sensorIds.Select(id => catalogue.FirstOrDefault(x => x.Id == id) ?? throw new ArgumentException("Okänd eller Unsupported sensor: " + id)).ToArray();
            if (selected.Any(x => x.Status != SensorStatus.Available)) throw new InvalidOperationException("Valda sensorer är inte tillgängliga. Kontrollera behörigheter och inventera igen.");
            session = sessionGuard.BeginSession();
            long token;
            lock (gate)
            {
                streams.Clear();
                foreach (var sensor in selected) streams.Add(sensor.Id, new(sensor, options, clock.GetTimestamp()));
                running = true;
                token = ++generation;
            }
            try
            {
                foreach (var sensor in selected)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    subscriptions.Add(await backend.SubscribeAsync(sensor, options, value => Receive(token, value), value => ReceiveState(token, value), cancellationToken));
                    Notify(new(sensor.Id, SensorStatus.Running));
                }
                timer = clock.CreateTimer(_ => CheckInterruptions(token), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
                logger.LogInformation("Sensorsession startad med {Count} sensorer, önskad frekvens {Frequency} Hz", selected.Length, options.FrequencyHz);
            }
            catch { await StopCoreAsync(SensorStatus.Error, "Start misslyckades; alla öppnade sensorer stoppades."); throw; }
        }
        finally { lifecycle.Release(); }
    }
    private void Receive(long token, SensorReading reading)
    {
        bool recovered;
        lock (gate)
        {
            if (!running || token != generation || !streams.TryGetValue(reading.SensorId, out var state)) return;
            var timestamp = reading.TimestampSource == SensorTimestampSource.AndroidElapsedRealtime ? reading.MonotonicTimestampNanoseconds : reading.TimestampUtc?.UtcTicks;
            if (!Enum.IsDefined(reading.TimestampSource) || reading.Kind != state.Descriptor.Kind || timestamp is null || timestamp < 0 ||
                (state.TimestampSource is not null && reading.TimestampSource != state.TimestampSource) ||
                (state.LastTimestamp is not null && timestamp <= state.LastTimestamp) || reading.Values.Count == 0 ||
                reading.Values.Any(x => !double.IsFinite(x.Value))) { state.Rejected++; return; }
            state.Count++;
            state.TimestampSource = reading.TimestampSource;
            state.FirstTimestamp ??= timestamp;
            state.LastTimestamp = timestamp;
            state.LastArrival = clock.GetTimestamp();
            recovered = state.Interrupted;
            state.Interrupted = false;
        }
        if (recovered) Notify(new(reading.SensorId, SensorStatus.Running, "Mätvärden återkommer."));
        foreach (var observer in ReadingReceived?.GetInvocationList() ?? [])
            try { ((Action<SensorReading>)observer)(reading); } catch (Exception exception) { logger.LogError(exception, "Sensorobservatör misslyckades"); }
    }
    private void ReceiveState(long token, SensorState state)
    {
        lock (gate)
        {
            if (!running || token != generation || !streams.TryGetValue(state.SensorId, out var stream)) return;
            stream.Interrupted = state.Status is SensorStatus.Interrupted or SensorStatus.Error or SensorStatus.PermissionRequired;
        }
        Notify(state);
    }
    private void CheckInterruptions(long token)
    {
        List<SensorState> changes = [];
        lock (gate)
        {
            if (!running || token != generation) return;
            foreach (var stream in streams.Values)
                if (stream.Descriptor.Capability.ReportingMode == SensorReportingMode.Continuous && !stream.Interrupted &&
                    clock.GetElapsedTime(stream.LastArrival) >= stream.Options.InterruptionTimeout)
                { stream.Interrupted = true; changes.Add(new(stream.Descriptor.Id, SensorStatus.Interrupted, "Inga nya mätvärden inom angiven tidsgräns.")); }
        }
        foreach (var state in changes) Notify(state);
    }
    public IReadOnlyList<SensorStatistics> GetStatistics()
    {
        lock (gate) return streams.Values.Select(x => new SensorStatistics(x.Descriptor.Id, x.Count, x.Rejected,
            x.Count < 2 || x.LastTimestamp <= x.FirstTimestamp ? null : (x.Count - 1) * (x.TimestampSource == SensorTimestampSource.AndroidElapsedRealtime ? 1e9 : 1e7) / (x.LastTimestamp - x.FirstTimestamp), x.Options.FrequencyHz)).ToArray();
    }
    private void Notify(SensorState state)
    {
        foreach (var observer in StateChanged?.GetInvocationList() ?? [])
            try { ((Action<SensorState>)observer)(state); } catch (Exception exception) { logger.LogError(exception, "Statusobservatör misslyckades"); }
    }
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await lifecycle.WaitAsync(cancellationToken);
        try { await StopCoreAsync(SensorStatus.Stopped, "Insamlingen stoppades."); }
        finally { lifecycle.Release(); }
    }
    private async Task StopCoreAsync(SensorStatus status, string detail)
    {
        string[] ids;
        lock (gate) { running = false; generation++; ids = streams.Keys.ToArray(); }
        if (timer is not null) { await timer.DisposeAsync(); timer = null; }
        foreach (var subscription in subscriptions)
            try { await subscription.DisposeAsync(); } catch (Exception exception) { logger.LogError(exception, "Sensor kunde inte stängas"); }
        subscriptions.Clear();
        session?.Dispose(); session = null;
        foreach (var id in ids) Notify(new(id, status, detail));
    }
    public async ValueTask DisposeAsync()
    {
        await lifecycle.WaitAsync();
        try { if (disposed) return; await StopCoreAsync(SensorStatus.Stopped, "Providern stängdes."); disposed = true; }
        finally { lifecycle.Release(); }
    }
}
