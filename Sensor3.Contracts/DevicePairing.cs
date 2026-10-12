namespace Sensor3.Contracts;
public sealed record PairingRequest(string Code, string DeviceId);
public sealed record PairingCredential(string Token);
public sealed record SavedDeviceCredential(string Server, string Token);
public interface IDevicePairingClient
{
    string? PendingLink { get; }
    Task<SavedDeviceCredential?> LoadAsync();
    Task<SavedDeviceCredential> PairAsync(string link, CancellationToken cancellationToken);
}
