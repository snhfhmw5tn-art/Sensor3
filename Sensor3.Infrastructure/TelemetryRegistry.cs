using System.Security.Cryptography;
using System.Text.Json;
using Sensor3.Contracts;
using Sensor3.Core;
using Sensor3.Sensors;
namespace Sensor3.Infrastructure;
public sealed class TelemetryRegistry(TimeProvider? timeProvider = null) : IRealtimeDiagnosticsSource, IRealtimeAnalysisSource, IDisposable
{
    private readonly object gate = new();
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly Dictionary<Guid, Session> sessions = [];
    private sealed class Session(TelemetryRegistration registration, TimeProvider clock) : IDisposable
    {
        public readonly TelemetryRegistration Registration = registration;
        public readonly SensorDiagnosticsStore Store = new(timeProvider: clock);
        public readonly AnalysisSession Analysis = new(registration.Catalogue, clock);
        public readonly long Started = clock.GetTimestamp();
        public long Sequence = -1, Packets, Bytes, Missing;
        public long? Dropped;
        public string? LastHash, ConnectionId;
        public DateTimeOffset LastArrival = clock.GetUtcNow();
        public string Status = "Connected";
        public void Dispose() => Analysis.Dispose();
    }
    public void Register(string authenticatedDevice, TelemetryRegistration registration, string? connectionId = null)
    {
        if (registration is null || registration.Client is null || registration.Client.Id != authenticatedDevice || authenticatedDevice.Length > 256 || !Enum.IsDefined(registration.Client.Platform)
            || registration.SessionId == Guid.Empty || !Enum.IsDefined(registration.Mode) || registration.Catalogue is null || registration.Catalogue.Count > 256 || registration.Iterations is null
            || registration.Catalogue.Any(x => x is null || string.IsNullOrWhiteSpace(x.Id) || x.Id.Length > 256 || !Enum.IsDefined(x.Kind) || !Enum.IsDefined(x.Status) || x.Capability is null || !Enum.IsDefined(x.Capability.ReportingMode))
            || registration.Catalogue.Select(x => x.Id).Distinct().Count() != registration.Catalogue.Count || !registration.Iterations.Select(x => x.Number).Order().SequenceEqual(Enumerable.Range(1, 18))
            || JsonSerializer.SerializeToUtf8Bytes(registration).Length > 512 * 1024) throw new InvalidDataException("Ogiltig session/enhetsidentitet/katalog.");
        if (registration.InitialContext is { } context) SensorEventValidation.ValidateContext(context);
        lock (gate)
        {
            if (sessions.TryGetValue(registration.SessionId, out var existing))
            {
                if (JsonSerializer.Serialize(existing.Registration) != JsonSerializer.Serialize(registration)) throw new UnauthorizedAccessException("Sessions-ID tillhör en annan registrering.");
                existing.Status = "Connected"; existing.ConnectionId = connectionId; existing.LastArrival = clock.GetUtcNow(); return;
            }
            foreach (var stale in sessions.Where(x => clock.GetUtcNow() - x.Value.LastArrival > TimeSpan.FromMinutes(15)).Select(x => x.Key).ToArray()) { sessions[stale].Dispose(); sessions.Remove(stale); }
            if (sessions.Count >= 64 || sessions.Values.Count(x => x.Registration.Client.Id == authenticatedDevice) >= 4) throw new InvalidOperationException("Sessionskapacitet uppnådd.");
            var session = new Session(registration, clock) { ConnectionId = connectionId };
            session.Store.SetClient(registration.Client, registration.Catalogue);
            if (registration.InitialContext is { } initial) session.Analysis.Configure(initial);
            sessions.Add(registration.SessionId, session);
        }
    }
    public bool Apply(string authenticatedDevice, TelemetryBatch batch)
    {
        lock (gate)
        {
            if (!sessions.TryGetValue(batch.SessionId, out var session) || session.Registration.Client.Id != authenticatedDevice) throw new UnauthorizedAccessException("Sessionen tillhör inte enheten.");
            if (batch.Sequence < 0 || batch.Events is null || batch.Events.Count > 256 || batch.ClientDroppedEvents < 0 || batch.Sequence > session.Sequence && batch.Sequence - session.Sequence > 1_000_000) throw new InvalidDataException("Ogiltigt paket/sekvens/loss-räknare.");
            foreach (var value in batch.Events) SensorEventValidation.Validate(value, session.Registration.Catalogue);
            var payload = JsonSerializer.SerializeToUtf8Bytes(batch);
            if (payload.Length > 512 * 1024) throw new InvalidDataException("Paketet är för stort.");
            var hash = Convert.ToHexString(SHA256.HashData(payload));
            if (batch.Sequence <= session.Sequence)
            {
                if (batch.Sequence == session.Sequence && hash != session.LastHash) throw new InvalidDataException("Konflikt mellan paket med samma sekvensnummer.");
                return true;
            }
            session.Missing += batch.Sequence - session.Sequence - 1;
            foreach (var value in batch.Events)
            {
                if (value.Reading is { } reading) session.Store.Receive(reading); else if (value.State is { } state) session.Store.SetState(state);
                session.Analysis.Receive(value);
            }
            session.Sequence = batch.Sequence; session.LastHash = hash; session.Packets++; session.Bytes += payload.Length;
            if (batch.ClientDroppedEvents is { } loss) session.Dropped = Math.Max(session.Dropped ?? 0, loss);
            session.LastArrival = clock.GetUtcNow(); session.Status = "Connected"; return false;
        }
    }
    public void Disconnect(string device, Guid sessionId, string? connectionId = null)
    {
        lock (gate) if (sessions.TryGetValue(sessionId, out var session) && session.Registration.Client.Id == device && session.ConnectionId == connectionId)
        {
            session.Status = "Disconnected"; session.Analysis.Interrupt();
            foreach (var descriptor in session.Registration.Catalogue.Where(x => x.Status == SensorStatus.Available)) session.Store.SetState(new(descriptor.Id, SensorStatus.Interrupted, "Native-klientanslutningen avslutades."));
        }
    }
    public IReadOnlyList<TelemetrySessionSummary> GetSessions()
    {
        lock (gate) return sessions.Select(x =>
        {
            var seconds = Math.Max(clock.GetElapsedTime(x.Value.Started).TotalSeconds, .001);
            var status = clock.GetUtcNow() - x.Value.LastArrival > TimeSpan.FromSeconds(15) && x.Value.Status == "Connected" ? "Stale" : x.Value.Status;
            return new TelemetrySessionSummary(x.Value.Registration.Client.Id, x.Key, x.Value.Registration.Mode, x.Value.Registration.Client.Build,
                new(x.Value.Packets, x.Value.Bytes, x.Value.Missing, x.Value.Dropped, x.Value.Packets / seconds, x.Value.Bytes / seconds, null, x.Value.LastArrival, status));
        }).ToArray();
    }
    private static SensorDiagnosticsSnapshot WithUsage(Session session)
    {
        var snapshot = session.Store.GetSnapshot();
        return snapshot with { Sensors = snapshot.Sensors.Select(sensor => sensor with { UsedByPositioning = session.Status == "Connected" && sensor.Status == SensorDiagnosticStatus.Active && session.Analysis.Steps.UsesSensorForPositioning(sensor.Descriptor.Id), CalculationUsage = session.Status == "Connected" && sensor.Status == SensorDiagnosticStatus.Active && session.Analysis.Steps.UsesSensorForPositioning(sensor.Descriptor.Id) ? "Aktiv källa till serverns fusion/carry/heading/PDR" : "Ej aktiv positioneringskälla" }).ToArray() };
    }
    public SensorDiagnosticsSnapshot GetDiagnostics(Guid sessionId) { lock (gate) return sessions.TryGetValue(sessionId, out var session) ? WithUsage(session) : new(null, []); }
    public RemoteAnalysisSnapshot? GetAnalysis(Guid sessionId)
    {
        lock (gate)
        {
            if (!sessions.TryGetValue(sessionId, out var session)) return null;
            if (session.Status != "Connected" || clock.GetUtcNow() - session.LastArrival > TimeSpan.FromSeconds(15)) session.Analysis.Interrupt();
            return session.Analysis.Snapshot();
        }
    }
    public void ConfigureAnalysis(Guid sessionId, AnalysisContext context)
    { lock (gate) { if (!sessions.TryGetValue(sessionId, out var session)) throw new InvalidOperationException("Sessionen saknas."); session.Analysis.Configure(context); } }
    public void ConfigureRadioMap(Guid sessionId, string json)
    { lock (gate) { if (!sessions.TryGetValue(sessionId, out var session)) throw new InvalidOperationException("Sessionen saknas."); session.Analysis.Radio.ImportMap(json); } }
    public void Dispose() { lock (gate) { foreach (var session in sessions.Values) session.Dispose(); sessions.Clear(); } }
}
