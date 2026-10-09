namespace Sensor3.Contracts;

public sealed record SignedUpdateEnvelope(string Payload, string Signature);
public sealed record AuthenticatedUpdate(string Nonce, DateTimeOffset ExpiresAtUtc, UpdateCheckResult Result);
public sealed record InstalledApplication(string Version, long BuildNumber, ClientPlatform Platform);
public interface IApplicationUpdateService
{
    InstalledApplication Installed { get; }
    UpdateCheckResult? Available { get; }
    string Status { get; }
    Task CheckAsync(CancellationToken cancellationToken = default);
    Task InstallAsync(CancellationToken cancellationToken = default);
}
public interface IUpdateInstaller
{
    Task StartAsync(string packagePath, CancellationToken cancellationToken);
}
public interface IUpdateSessionGuard
{
    bool IsSessionActive { get; }
    IDisposable AcquireInstallation();
}
// Sensor collection in iteration 04 must acquire this same guard for its entire session.
public sealed class UpdateSessionGuard : IUpdateSessionGuard
{
    private readonly object gate = new();
    private int active;
    private bool installing;
    public bool IsSessionActive { get { lock (gate) return active > 0; } }
    public IDisposable BeginSession()
    {
        lock (gate)
        {
            if (installing) throw new InvalidOperationException("Uppdatering pågår.");
            active++;
            return new Lease(this, false);
        }
    }
    public IDisposable AcquireInstallation()
    {
        lock (gate)
        {
            if (active > 0 || installing) throw new InvalidOperationException("Avsluta den aktiva sensorsessionen före uppdatering.");
            installing = true;
            return new Lease(this, true);
        }
    }
    private sealed class Lease(UpdateSessionGuard owner, bool installation) : IDisposable
    {
        private UpdateSessionGuard? guard = owner;
        public void Dispose()
        {
            var value = Interlocked.Exchange(ref guard, null);
            if (value is null) return;
            lock (value.gate) { if (installation) value.installing = false; else value.active--; }
        }
    }
}
