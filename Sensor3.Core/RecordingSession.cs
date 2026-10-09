using System.Diagnostics;
using Sensor3.Contracts;
namespace Sensor3.Core;
public sealed class RecordingSession : IRecordingSession, IDisposable
{
    private readonly object gate = new();
    private readonly ISensorProvider provider;
    private readonly NativeObservationBus bus;
    private readonly StepSession session;
    private readonly List<RecordedFrame> frames = [];
    private SensorRecording? recording;
    private long started, recordedBytes;
    private readonly UpdateSessionGuard guard;
    private readonly IForkliftMotionEstimator vehicle;
    private readonly IRadioPositionSession radio;
    private IDisposable? lease;
    private bool active, truncated;
    public bool IsRecording { get { lock (gate) return active; } }
    public int FrameCount { get { lock (gate) return frames.Count; } }
    public RecordingSession(ISensorProvider provider, NativeObservationBus bus, StepSession session, UpdateSessionGuard guard, IForkliftMotionEstimator vehicle, IRadioPositionSession radio)
    { this.guard = guard; this.vehicle = vehicle; this.radio = radio; this.provider = provider; this.bus = bus; this.session = session; provider.ReadingReceived += Reading; provider.StateChanged += State; bus.LocationReceived += Location; bus.WifiReceived += Wifi; bus.BluetoothReceived += Bluetooth; }
    public async Task StartAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120) throw new ArgumentException("Ange ett sessionsnamn, max 120 tecken.");
        var catalogue = await provider.DiscoverAsync(cancellationToken);
        lock (gate)
        {
            if (active) throw new InvalidOperationException("Inspelning pågår.");
            var heading = session.GetHeading();
            lease = guard.BeginSession();
            recording = new(1, name, false, DateTimeOffset.UtcNow, catalogue, [], false, heading.Confidence > 0 ? heading.Radians : null,
                session.GetPosition().Position, session.IsForkliftDeclared, session.DeclaredCarrying, vehicle.GetGpsOrigin(), System.Text.Json.JsonSerializer.Deserialize<RadioMap>(radio.ExportMap()));
            frames.Clear(); recordedBytes = 0; truncated = false; started = Stopwatch.GetTimestamp(); active = true;
        }
    }
    private void Append(TelemetryEvent value)
    {
        lock (gate) { if (!active) return; var size = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value).LongLength + 200;
            if (frames.Count >= 60000 || recordedBytes + size > 8_000_000) { truncated = true; active = false; lease?.Dispose(); lease = null; return; } recordedBytes += size; frames.Add(new(Stopwatch.GetElapsedTime(started).TotalSeconds, value)); }
    }
    private void Reading(SensorReading value) => Append(new(value with { Values = value.Values.ToArray() }));
    private void State(SensorState value) => Append(new(State: value));
    private void Location(LocationObservation value) => Append(new(Location: value));
    private void Wifi(IReadOnlyList<WifiObservation> value) => Append(new(Wifi: value.ToArray()));
    private void Bluetooth(IReadOnlyList<BluetoothObservation> value) => Append(new(Bluetooth: value.ToArray()));
    public SensorRecording Stop() { lock (gate) { active = false; lease?.Dispose(); lease = null; return (recording ?? throw new InvalidOperationException("Ingen inspelning finns.")) with { Frames = frames.ToArray(), Truncated = truncated }; } }
    public void Dispose() { lock (gate) { active = false; lease?.Dispose(); lease = null; } provider.ReadingReceived -= Reading; provider.StateChanged -= State; bus.LocationReceived -= Location; bus.WifiReceived -= Wifi; bus.BluetoothReceived -= Bluetooth; }
}
