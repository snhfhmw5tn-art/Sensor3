using System.Security.Cryptography;
using System.Text.Json;
using Sensor3.Contracts;
using Sensor3.Sensors;

namespace Sensor3.Infrastructure;

public sealed class TelemetryRegistry(TimeProvider? timeProvider = null) : IRealtimeDiagnosticsSource
{
    private readonly object gate = new();
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly Dictionary<Guid, Session> sessions = [];
    private sealed class Session(TelemetryRegistration registration, TimeProvider clock)
    {
        public readonly TelemetryRegistration Registration = registration;
        public readonly SensorDiagnosticsStore Store = new(timeProvider: clock);
        public readonly long Started = clock.GetTimestamp();
        public long Sequence = -1;
        public long Packets;
        public long Bytes;
        public long Missing;
        public string? LastHash;
        public DateTimeOffset LastArrival = clock.GetUtcNow();
        public string Status = "Connected";
        public string? ConnectionId;
    }
    public void Register(string authenticatedDevice, TelemetryRegistration registration, string? connectionId = null)
    {
        if (registration.Client.Id != authenticatedDevice || registration.SessionId == Guid.Empty || !Enum.IsDefined(registration.Mode) ||
            registration.Catalogue.Count > 256 || !registration.Iterations.Select(x => x.Number).Order().SequenceEqual(Enumerable.Range(1, 18))) throw new InvalidDataException("Ogiltig session eller enhetsidentitet.");
        lock (gate)
        {
            if (sessions.TryGetValue(registration.SessionId, out var existing))
            {
                if (JsonSerializer.Serialize(existing.Registration) != JsonSerializer.Serialize(registration)) throw new UnauthorizedAccessException("Sessions-ID tillhör en annan registrering.");
                existing.Status = "Connected"; existing.ConnectionId = connectionId; existing.LastArrival = clock.GetUtcNow(); return;
            }
            foreach (var stale in sessions.Where(x => clock.GetUtcNow() - x.Value.LastArrival > TimeSpan.FromMinutes(15)).Select(x => x.Key).ToArray()) sessions.Remove(stale);
            if (sessions.Count >= 64 || sessions.Values.Count(x => x.Registration.Client.Id == authenticatedDevice) >= 4) throw new InvalidOperationException("Sessionskapacitet uppnådd.");
            var session = new Session(registration, clock);
            session.ConnectionId = connectionId;
            session.Store.SetClient(registration.Client, registration.Catalogue);
            sessions.Add(registration.SessionId, session);
        }
    }
    public bool Apply(string authenticatedDevice, TelemetryBatch batch)
    {
        lock (gate)
        {
            if (!sessions.TryGetValue(batch.SessionId, out var session) || session.Registration.Client.Id != authenticatedDevice) throw new UnauthorizedAccessException("Sessionen tillhör inte enheten.");
            if (batch.Sequence < 0 || batch.Events.Count > 256 || batch.Events.Any(x => (x.Reading is null) == (x.State is null) ||
                x.Reading is { } reading && (reading.Values.Count is < 1 or > 64 || reading.Values.Any(v => !double.IsFinite(v.Value)) || !session.Registration.Catalogue.Any(d => d.Id == reading.SensorId)) ||
                x.State is { } state && !session.Registration.Catalogue.Any(d => d.Id == state.SensorId))) throw new InvalidDataException("Ogiltigt sensorpaket.");
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
                if (value.Reading is { } reading) session.Store.Receive(reading); else session.Store.SetState(value.State!);
            session.Sequence = batch.Sequence; session.LastHash = hash; session.Packets++; session.Bytes += payload.Length;
            session.LastArrival = clock.GetUtcNow(); session.Status = "Connected";
            return false;
        }
    }
    public void Disconnect(string device, Guid sessionId, string? connectionId = null)
    {
        lock (gate) if (sessions.TryGetValue(sessionId, out var session) && session.Registration.Client.Id == device && session.ConnectionId == connectionId)
        {
            session.Status = "Disconnected";
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
                new(x.Value.Packets, x.Value.Bytes, x.Value.Missing, 0, x.Value.Packets / seconds, x.Value.Bytes / seconds, null, x.Value.LastArrival, status));
        }).ToArray();
    }
    public SensorDiagnosticsSnapshot GetDiagnostics(Guid sessionId)
    {
        lock (gate) return sessions.TryGetValue(sessionId, out var session) ? session.Store.GetSnapshot() : new(null, []);
    }
}
