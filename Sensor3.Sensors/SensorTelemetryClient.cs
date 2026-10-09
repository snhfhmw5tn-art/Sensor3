using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Sensor3.Contracts;

namespace Sensor3.Sensors;

public sealed class SensorTelemetryClient(string deviceId, ISensorProvider provider, SensorDiagnosticsStore store,
    BuildInfo build, IReadOnlyList<IterationInfo> iterations, ILogger<SensorTelemetryClient> logger) : ITelemetryClient
{
    private readonly SemaphoreSlim lifecycle = new(1);
    private Channel<TelemetryEvent>? events;
    private CancellationTokenSource? lifetime;
    private HubConnection? connection;
    private Task? worker;
    private long packets, bytes, dropped;
    private long started;
    private double? roundTrip;
    private DateTimeOffset? last;
    private string status = "Disconnected";
    public string DeviceId => deviceId;
    public BuildInfo? ServerBuild { get; private set; }
    public TelemetryStatistics GetStatistics()
    {
        var seconds = started == 0 ? 1 : Math.Max(Stopwatch.GetElapsedTime(started).TotalSeconds, .001);
        return new(Interlocked.Read(ref packets), Interlocked.Read(ref bytes), 0, Interlocked.Read(ref dropped), packets / seconds, bytes / seconds, roundTrip, last, status);
    }
    public async Task ConnectAsync(Uri endpoint, string token, TelemetryMode mode, CancellationToken cancellationToken = default)
    {
        if (endpoint.Scheme != "https" || endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0)
            throw new ArgumentException("Ange en HTTPS-server utan inloggningsuppgifter eller query-parametrar.");
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("Enhetstoken krävs.");
        await lifecycle.WaitAsync(cancellationToken);
        try
        {
            if (lifetime is not null) throw new InvalidOperationException("Koppla från den nuvarande sessionen först.");
            var catalogue = store.GetSnapshot().Sensors.Select(x => x.Descriptor).ToArray();
            if (catalogue.Length == 0) catalogue = (await provider.DiscoverAsync(cancellationToken)).ToArray();
            var registration = new TelemetryRegistration(new(deviceId, build.TargetPlatform.Contains("android", StringComparison.OrdinalIgnoreCase) ? ClientPlatform.Android : ClientPlatform.Windows, build), Guid.NewGuid(), mode, catalogue, iterations);
            var hub = new HubConnectionBuilder().WithUrl(new Uri(endpoint.AbsoluteUri.TrimEnd('/') + "/hubs/sensors"), options =>
            { options.Headers["X-Sensor3-DeviceId"] = deviceId; options.AccessTokenProvider = () => Task.FromResult<string?>(token); }).Build();
            try
            {
                await hub.StartAsync(cancellationToken);
                ServerBuild = await hub.InvokeAsync<BuildInfo>("Register", registration, cancellationToken);
            }
            catch { await hub.DisposeAsync(); throw; }
            connection = hub; lifetime = new();
            events = Channel.CreateBounded<TelemetryEvent>(new BoundedChannelOptions(4096) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });
            packets = bytes = dropped = 0; started = Stopwatch.GetTimestamp(); status = "Connected";
            provider.ReadingReceived += Receive; provider.StateChanged += State;
            worker = SendAsync(hub, registration, events.Reader, lifetime.Token);
        }
        finally { lifecycle.Release(); }
    }
    private void Receive(SensorReading reading) => Enqueue(new(reading with { Values = reading.Values.ToArray() }));
    private void State(SensorState state) => Enqueue(new(State: state));
    private void Enqueue(TelemetryEvent value)
    {
        if (events?.Writer.TryWrite(value) != true) Interlocked.Increment(ref dropped);
    }
    private async Task SendAsync(HubConnection hub, TelemetryRegistration registration, ChannelReader<TelemetryEvent> reader, CancellationToken cancellationToken)
    {
        long sequence = 0;
        TelemetryBatch? pending = null;
        var delay = registration.Mode == TelemetryMode.Research ? 100 : 250;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(delay, cancellationToken);
                if (pending is null)
                {
                    var batch = new List<TelemetryEvent>();
                    while (batch.Count < 256 && reader.TryRead(out var item)) batch.Add(item);
                    pending = new(registration.SessionId, sequence, DateTimeOffset.UtcNow, batch);
                }
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(10));
                    if (hub.State == HubConnectionState.Disconnected)
                    {
                        await hub.StartAsync(timeout.Token);
                        ServerBuild = await hub.InvokeAsync<BuildInfo>("Register", registration, timeout.Token);
                    }
                    var timestamp = Stopwatch.GetTimestamp();
                    var ack = await hub.InvokeAsync<TelemetryAcknowledgement>("Send", pending, timeout.Token);
                    if (ack.Sequence != sequence) throw new InvalidDataException("Felaktig kvittens.");
                    roundTrip = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;
                    Interlocked.Increment(ref packets); Interlocked.Add(ref bytes, JsonSerializer.SerializeToUtf8Bytes(pending).Length);
                    last = DateTimeOffset.UtcNow; ServerBuild = ack.ServerBuild; status = "Connected"; sequence++; pending = null;
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
                {
                    status = "Reconnecting"; logger.LogWarning("Sensoranslutningen avbröts: {Type}", exception.GetType().Name);
                    try { await hub.StopAsync(cancellationToken); } catch (Exception) when (!cancellationToken.IsCancellationRequested) { }
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await lifecycle.WaitAsync(cancellationToken);
        try
        {
            if (lifetime is null) return;
            provider.ReadingReceived -= Receive; provider.StateChanged -= State;
            lifetime.Cancel();
            if (worker is not null) await worker;
            if (events is not null) while (events.Reader.TryRead(out _)) dropped++;
            if (connection is not null) await connection.DisposeAsync();
            lifetime.Dispose(); lifetime = null; events = null; connection = null; worker = null; status = "Disconnected";
        }
        finally { lifecycle.Release(); }
    }
    public async ValueTask DisposeAsync() => await DisconnectAsync();
}
