using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Sensor3.Infrastructure;
public sealed record PairingInvitation(string Code, DateTimeOffset ExpiresAt);
public sealed class DevicePairingStore(string directory, TimeProvider clock)
{
    private readonly object gate = new();
    private readonly Dictionary<string, DateTimeOffset> invitations = [];
    private string CredentialFile => Path.Combine(directory, "device-hashes.json");
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public PairingInvitation Create()
    {
        lock (gate)
        {
            foreach (var key in invitations.Where(x => x.Value <= clock.GetUtcNow()).Select(x => x.Key).ToArray()) invitations.Remove(key);
            if (invitations.Count >= 100) throw new InvalidOperationException("För många aktiva koder. Vänta fem minuter.");
            var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var expires = clock.GetUtcNow().AddMinutes(5);
            invitations.Add(Hash(code), expires);
            return new(code, expires);
        }
    }
    public string? Redeem(string code, string deviceId)
    {
        if (code.Length != 64 || deviceId.Length != 32 || !Guid.TryParseExact(deviceId, "N", out _)) return null;
        lock (gate)
        {
            var key = Hash(code);
            if (!invitations.TryGetValue(key, out var expires) || expires <= clock.GetUtcNow()) return null;
            Directory.CreateDirectory(directory);
            using var fileLock = new FileStream(Path.Combine(directory, "device-hashes.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var hashes = ReadHashes();
            if (hashes.ContainsKey(deviceId)) return null;
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            hashes.Add(deviceId, Hash(token));
            var temporary = CredentialFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(hashes));
                File.Move(temporary, CredentialFile, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            invitations.Remove(key);
            return token;
        }
    }
    public string? GetHash(string deviceId) { lock (gate) return ReadHashes().GetValueOrDefault(deviceId); }
    private Dictionary<string, string> ReadHashes() => File.Exists(CredentialFile)
        ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(CredentialFile)) ?? throw new InvalidDataException("Enhetsregistret är skadat.") : [];
}
