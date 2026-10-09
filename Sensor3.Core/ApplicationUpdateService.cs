using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sensor3.Contracts;

namespace Sensor3.Core;

public sealed record ApplicationUpdateOptions
{
    public string ServerUrl { get; init; } = "";
    public string ManifestPublicKeyPem { get; init; } = "";
    public ReleaseChannel Channel { get; init; } = ReleaseChannel.Stable;
    public long MaximumPackageBytes { get; init; } = 100 * 1024 * 1024;
}

public sealed class ApplicationUpdateService(HttpClient http, ApplicationUpdateOptions options, InstalledApplication installed,
    IUpdateInstaller installer, IUpdateSessionGuard guard, string cacheDirectory) : IApplicationUpdateService
{
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly SemaphoreSlim operation = new(1, 1);
    private DateTimeOffset expires;
    public InstalledApplication Installed { get; } = installed;
    public UpdateCheckResult? Available { get; private set; }
    public string Status { get; private set; } = "Inte kontrollerad";

    private Uri Server()
    {
        if (!Uri.TryCreate(options.ServerUrl, UriKind.Absolute, out var server) || server.Scheme != "https" ||
            !string.IsNullOrEmpty(server.UserInfo) || string.IsNullOrWhiteSpace(options.ManifestPublicKeyPem))
            throw new InvalidOperationException("Uppdateringar kräver konfigurerad HTTPS-server och betrodd manifestnyckel.");
        return server;
    }

    public async Task CheckAsync(CancellationToken cancellationToken = default)
    {
        await operation.WaitAsync(cancellationToken);
        try
        {
            await CheckSelectionAsync(cancellationToken);
        }
        finally { operation.Release(); }
    }

    private async Task CheckSelectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            Available = null;
            Status = "Kontrollerar";
            var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var url = new Uri(Server(), $"/api/updates/authenticated?platform={Installed.Platform}&channel={options.Channel}&version={Uri.EscapeDataString(Installed.Version)}&buildNumber={Installed.BuildNumber}&nonce={nonce}");
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            await CopyBoundedAsync(body, buffer, 65536, cancellationToken);
            var envelope = JsonSerializer.Deserialize<SignedUpdateEnvelope>(buffer.ToArray(), JsonOptions) ?? throw new InvalidDataException("Manifest saknas.");
            using var rsa = RSA.Create();
            rsa.ImportFromPem(options.ManifestPublicKeyPem);
            if (rsa.KeySize < 2048) throw new InvalidDataException("Manifestnyckeln är för svag.");
            var payload = Convert.FromBase64String(envelope.Payload);
            if (!rsa.VerifyData(payload, Convert.FromBase64String(envelope.Signature), HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
                throw new InvalidDataException("Manifestets signatur är ogiltig.");
            var update = JsonSerializer.Deserialize<AuthenticatedUpdate>(payload, JsonOptions) ?? throw new InvalidDataException("Manifest saknas.");
            if (update.Nonce != nonce || update.ExpiresAtUtc <= DateTimeOffset.UtcNow || update.ExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(10))
                throw new InvalidDataException("Manifestet är gammalt eller gäller en annan förfrågan.");
            ValidateSelection(update.Result);
            Available = update.Result;
            expires = update.ExpiresAtUtc;
            Status = update.Result.UpdateAvailable ? "Uppdatering tillgänglig" : "Ingen kompatibel uppdatering";
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException or JsonException or CryptographicException or ArgumentException or FormatException or InvalidOperationException or OperationCanceledException)
        { Status = exception is OperationCanceledException ? "Kontrollen avbröts" : "Kontrollen misslyckades: " + exception.Message; throw; }
    }

    private void ValidateSelection(UpdateCheckResult result)
    {
        if (!result.UpdateAvailable) return;
        var release = result.Release ?? throw new InvalidDataException("Release saknas.");
        var m = release.Manifest;
        var compare = ApplicationVersion.Parse(m.Version).CompareTo(ApplicationVersion.Parse(Installed.Version));
        if (release.IsRevoked || m.Platform != Installed.Platform || m.Channel != options.Channel || compare < 0 ||
            (compare == 0 && m.BuildNumber <= Installed.BuildNumber) || m.BuildNumber <= Installed.BuildNumber ||
            release.Artifact.FileSize <= 0 || release.Artifact.FileSize > options.MaximumPackageBytes ||
            release.Artifact.Sha256.Length != 64 || !release.Artifact.Sha256.All(char.IsAsciiHexDigit) ||
            !release.Artifact.Sha256.Equals(m.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Releasen har fel plattform, kanal, version eller filmetadata.");
    }

    public async Task InstallAsync(CancellationToken cancellationToken = default)
    {
        await operation.WaitAsync(cancellationToken);
        string? path = null;
        try
        {
            using var installation = guard.AcquireInstallation();
            var release = Available?.UpdateAvailable == true ? Available.Release : null;
            if (release is null || expires <= DateTimeOffset.UtcNow) throw new InvalidOperationException("Kontrollera uppdateringar igen före installation.");
            ValidateSelection(Available!);
            Directory.CreateDirectory(cacheDirectory);
            path = Path.Combine(cacheDirectory, Guid.NewGuid().ToString("N") + (Installed.Platform == ClientPlatform.Android ? ".apk" : ".msix"));
            Status = "Hämtar och kontrollerar installationsfil";
            using var response = await http.GetAsync(new Uri(Server(), $"/api/releases/{release.Id}/download"), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                await CopyBoundedAsync(source, target, release.Artifact.FileSize, cancellationToken);
                if (target.Length != release.Artifact.FileSize) throw new InvalidDataException("Installationsfilen är ofullständig.");
                target.Position = 0;
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(target, cancellationToken));
                if (!hash.Equals(release.Artifact.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Installationsfilens checksumma är fel.");
            }
            if (guard.IsSessionActive) throw new InvalidOperationException("En sensorsession pågår. Installation stoppad.");
            // Re-authenticate the current selection to reject revocation during download.
            await CheckSelectionAsync(cancellationToken);
            if (Available?.UpdateAvailable != true || Available.Release?.Id != release.Id ||
                Available.Release.Artifact.Sha256 != release.Artifact.Sha256)
                throw new InvalidOperationException("Releasen har ändrats eller återkallats. Kontrollera igen.");
            cancellationToken.ThrowIfCancellationRequested();
            await installer.StartAsync(path, cancellationToken);
            path = null; // OS installer still needs access; files live in the platform cache.
            Status = "Installationsdialogen har öppnats";
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException or InvalidOperationException or OperationCanceledException)
        { Status = exception is OperationCanceledException ? "Hämtningen avbröts" : "Installation stoppad: " + exception.Message; throw; }
        finally { if (path is not null && File.Exists(path)) File.Delete(path); operation.Release(); }
    }

    private static async Task CopyBoundedAsync(Stream source, Stream target, long maximum, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        int count;
        while ((count = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += count;
            if (total > maximum) throw new InvalidDataException("Svaret överskrider tillåten storlek.");
            await target.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
        }
    }
}
