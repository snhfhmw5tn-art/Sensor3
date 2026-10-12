using Microsoft.Extensions.Logging;
using Sensor3.Contracts;

namespace Sensor3.Sensors;

public sealed class PositioningSession(ISensorProvider provider, IStepSession steps, SensorDiagnosticsStore store,
    IRecordingSession recorder, ITelemetryClient telemetry, BuildInfo build, ILogger<PositioningSession> logger,
    ISensorPermissionService? permissions = null) : IAsyncDisposable
{
    private readonly DiagnosticSubscription subscription = new(provider, store);
    private readonly SemaphoreSlim lifecycle = new(1);
    private bool ownsSession;
    private bool recordingStarted;
    public bool IsActive => ownsSession;
    public bool IsRunning => ownsSession && provider.IsRunning;
    public int FrameCount => recorder.FrameCount;
    public bool IsRecording => recorder.IsRecording;
    public SensorRecording? LastRecording { get; private set; }
    public async Task<int> StartAsync(Uri endpoint, string token, TelemetryMode mode, CancellationToken cancellationToken = default)
    {
        await lifecycle.WaitAsync(cancellationToken);
        try
        {
            if (ownsSession || provider.IsRunning || recorder.IsRecording) throw new InvalidOperationException("Stoppa pågående insamling eller inspelning först.");
            var connected = telemetry.GetStatistics().Status is "Connected" or "Reconnecting";
            if (endpoint.Scheme != "https" || !connected && string.IsNullOrWhiteSpace(token)) throw new ArgumentException("Ange HTTPS-server och enhetstoken före start.");
            var catalogue = await provider.DiscoverAsync(cancellationToken);
            var required = catalogue.Where(x => x.Status == SensorStatus.PermissionRequired).ToArray();
            if (permissions is not null && required.Length > 0)
            {
                await permissions.RequestAsync(required, cancellationToken);
                catalogue = await provider.DiscoverAsync(cancellationToken);
            }
            var selected = catalogue.Where(x => x.Status == SensorStatus.Available).ToArray();
            if (selected.Length == 0) throw new InvalidOperationException("Inga tillgängliga sensorer finns. Kontrollera enhetens behörigheter.");
            store.SetClient(new(telemetry.DeviceId, build.TargetPlatform.Contains("android", StringComparison.OrdinalIgnoreCase) ? ClientPlatform.Android : ClientPlatform.Windows, build), catalogue);
            steps.Configure(selected);
            if (!connected) await telemetry.DisconnectAsync(cancellationToken);
            ownsSession = true;
            try
            {
                if (!connected) await telemetry.ConnectAsync(endpoint, token, mode, cancellationToken);
                await recorder.StartAsync($"Positionering {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}", cancellationToken);
                recordingStarted = true;
                store.BeginSession(selected.Select(x => x.Id).ToArray(), 50);
                await provider.StartAsync(selected.Select(x => x.Id).ToArray(), new() { FrequencyHz = 50 }, cancellationToken);
                logger.LogInformation("Positionering och serverloggning startade med {Count} native-sensorer", selected.Length);
                return selected.Length;
            }
            catch
            {
                try { await StopCoreAsync(); }
                catch (Exception exception) { logger.LogWarning(exception, "Positioneringsstart kunde inte återställas fullständigt"); }
                throw;
            }
        }
        finally { lifecycle.Release(); }
    }
    public async Task StopAsync()
    {
        await lifecycle.WaitAsync();
        try { await StopCoreAsync(); }
        finally { lifecycle.Release(); }
    }
    private async Task StopCoreAsync()
    {
        if (!ownsSession) return;
        try { await provider.StopAsync(); }
        finally
        {
            try { if (recordingStarted) LastRecording = recorder.Stop(); }
            finally
            {
                try { await telemetry.DisconnectAsync(); }
                finally { recordingStarted = false; ownsSession = false; }
            }
        }
    }
    public async ValueTask DisposeAsync() { try { await StopAsync(); } finally { subscription.Dispose(); } }
    private sealed class DiagnosticSubscription : IDisposable
    {
        private readonly ISensorProvider provider;
        private readonly SensorDiagnosticsStore store;
        public DiagnosticSubscription(ISensorProvider provider, SensorDiagnosticsStore store)
        {
            this.provider = provider; this.store = store;
            provider.ReadingReceived += store.Receive; provider.StateChanged += store.SetState;
        }
        public void Dispose() { provider.ReadingReceived -= store.Receive; provider.StateChanged -= store.SetState; }
    }
}
