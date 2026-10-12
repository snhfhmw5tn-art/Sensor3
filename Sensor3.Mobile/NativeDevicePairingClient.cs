using System.Net.Http.Json;
using System.Text.Json;
using Sensor3.Contracts;
namespace Sensor3;
public sealed class NativeDevicePairingClient(ITelemetryClient client) : IDevicePairingClient
{
    public static string? IncomingLink { get; set; }
    public string? PendingLink => IncomingLink;
    private string Key => "Sensor3.Pairing." + client.DeviceId;
    public async Task<SavedDeviceCredential?> LoadAsync()
    {
        var value = await SecureStorage.Default.GetAsync(Key);
        return value is null ? null : JsonSerializer.Deserialize<SavedDeviceCredential>(value);
    }
    public async Task<SavedDeviceCredential> PairAsync(string link, CancellationToken cancellationToken)
    {
        var parsed = PairingLink.Parse(link);
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) };
        using var response = await http.PostAsJsonAsync(new Uri(parsed.Server, "/api/pairing/redeem"), new PairingRequest(parsed.Code, client.DeviceId), cancellationToken);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Registreringen misslyckades ({(int)response.StatusCode}). Koden kan vara utgången eller redan använd.");
        var result = await response.Content.ReadFromJsonAsync<PairingCredential>(cancellationToken) ?? throw new InvalidOperationException("Servern gav inget registreringssvar.");
        if (result.Token.Length != 64 || result.Token.Any(c => !Uri.IsHexDigit(c))) throw new InvalidOperationException("Servern gav en ogiltig token.");
        var saved = new SavedDeviceCredential(parsed.Server.AbsoluteUri, result.Token);
        await SecureStorage.Default.SetAsync(Key, JsonSerializer.Serialize(saved));
        IncomingLink = null;
        return saved;
    }
}
