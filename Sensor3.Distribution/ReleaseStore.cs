using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Sensor3.Contracts;

namespace Sensor3.Distribution;

public sealed record DistributionOptions
{
    public string ManifestSigningKeyPath { get; init; } = "";
    public string AndroidApkSignerPath { get; init; } = "";
    public string AndroidAaptPath { get; init; } = "";
    public string AndroidCertificateSha256 { get; init; } = "";
    public string JavaPath { get; init; } = "";
    public string WindowsSignToolPath { get; init; } = "";
    public string PublicBaseUrl { get; init; } = "";
    public string StorageRoot { get; init; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sensor3", "distribution");
    public long MaximumArtifactBytes { get; init; } = 100 * 1024 * 1024;
    public string AdminUsername { get; init; } = "";
    public string AdminPasswordHash { get; init; } = "";
}

public sealed class ReleaseStore
{
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly DistributionOptions options;
    private readonly ILogger<ReleaseStore> logger;
    private readonly string root;
    private readonly IReleasePackageVerifier verifier;
    public ReleaseStore(DistributionOptions options, ILogger<ReleaseStore> logger, IReleasePackageVerifier? verifier = null)
    {
        if (options.MaximumArtifactBytes is < 1 or > 1024L * 1024 * 1024) throw new ArgumentException("Ogiltig storleksgräns.");
        this.options = options;
        this.logger = logger;
        this.verifier = verifier ?? new PackageVerifier(options);
        root = Path.GetFullPath(options.StorageRoot);
        Directory.CreateDirectory(root);
    }
    public async Task<IReadOnlyList<ApplicationRelease>> ListAsync(CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(root, "releases.json");
        if (!File.Exists(path)) return [];
        // Atomic replacement lets readers use the preceding complete catalogue during publication.
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        try { return await JsonSerializer.DeserializeAsync<ApplicationRelease[]>(stream, JsonOptions, cancellationToken) ?? throw new InvalidDataException("Releasekatalogen är tom eller skadad."); }
        catch (JsonException exception) { throw new InvalidDataException("Releasekatalogen är skadad.", exception); }
    }

    public async Task<ApplicationRelease> PublishAsync(ReleaseManifest manifest, string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        ValidateManifest(manifest);
        var extension = manifest.Platform == ClientPlatform.Android ? ".apk" : ".msix";
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 150 || fileName.Contains('/') || fileName.Contains('\\') ||
            !Regex.IsMatch(fileName, @"^[a-zA-Z0-9][a-zA-Z0-9._-]*$", RegexOptions.CultureInvariant) || !fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Filnamn måste vara ett enkelt .apk- eller .msix-namn för vald plattform.");
        var id = Guid.NewGuid();
        var temporary = Path.Combine(root, $"{id:N}.upload");
        var artifactPath = Path.Combine(root, $"{id:N}.package");
        var committed = false;
        try
        {
            long length = 0;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous))
            {
                var buffer = new byte[65536];
                int read;
                while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    length += read;
                    if (length > options.MaximumArtifactBytes) throw new ArgumentException("Installationsfilen överskrider storleksgränsen.");
                    hash.AppendData(buffer.AsSpan(0, read));
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
                if (length == 0) throw new ArgumentException("Installationsfilen är tom.");
            }
            var checksum = Convert.ToHexStringLower(hash.GetHashAndReset());
            if (!string.Equals(checksum, manifest.ExpectedSha256, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("SHA-256 stämmer inte med manifestet.");
            ValidatePackage(temporary, manifest.Platform);
            await verifier.VerifyAsync(temporary, manifest, cancellationToken);
            await using var catalogueLock = await LockAsync(cancellationToken);
            var releases = (await ListAsync(cancellationToken)).ToList();
            if (releases.Any(x => x.Manifest.Platform == manifest.Platform && x.Manifest.Channel == manifest.Channel &&
                x.Manifest.Version == manifest.Version && x.Manifest.BuildNumber == manifest.BuildNumber)) throw new ArgumentException("Version/build finns redan i denna kanal.");
            if (releases.Any(x => x.Manifest.Platform == manifest.Platform && x.Manifest.BuildNumber >= manifest.BuildNumber))
                throw new ArgumentException("Buildnummer måste öka för plattformen, även mellan kanaler.");
            var release = new ApplicationRelease(id, manifest with { GitCommitDateUtc = manifest.GitCommitDateUtc.ToUniversalTime(), ExpectedSha256 = checksum },
                new(fileName, length, checksum, manifest.Platform == ClientPlatform.Android ? "application/vnd.android.package-archive" : "application/msix"), DateTimeOffset.UtcNow);
            File.Move(temporary, artifactPath);
            releases.Add(release);
            await SaveAsync(releases, cancellationToken);
            committed = true;
            logger.LogInformation("Release {ReleaseId} publicerad: {Platform} {Version} build {Build}", id, manifest.Platform, manifest.Version, manifest.BuildNumber);
            return release;
        }
        finally
        {
            File.Delete(temporary);
            if (!committed) File.Delete(artifactPath);
        }
    }

    public async Task<bool> RevokeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var catalogueLock = await LockAsync(cancellationToken);
        var releases = (await ListAsync(cancellationToken)).ToList();
        var index = releases.FindIndex(x => x.Id == id);
        if (index < 0) return false;
        releases[index] = releases[index] with { IsRevoked = true };
        await SaveAsync(releases, cancellationToken);
        logger.LogInformation("Release {ReleaseId} återkallad", id);
        return true;
    }

    public async Task<(ApplicationRelease Release, FileStream Content)?> OpenDownloadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Hold the catalogue lock until validation/open completes, so revocation has a defined ordering.
        await using var catalogueLock = await LockAsync(cancellationToken);
        var release = (await ListAsync(cancellationToken)).FirstOrDefault(x => x.Id == id && !x.IsRevoked);
        if (release is null) return null;
        var path = Path.Combine(root, $"{id:N}.package");
        if (!File.Exists(path)) return null;
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
        try
        {
            var checksum = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
            if (stream.Length != release.Artifact.FileSize || checksum != release.Artifact.Sha256)
                throw new InvalidDataException("Installationsfilens integritet kunde inte verifieras.");
            stream.Position = 0;
            return (release, stream);
        }
        catch { await stream.DisposeAsync(); throw; }
    }

    public static void ValidateManifest(ReleaseManifest manifest)
    {
        var version = ApplicationVersion.Parse(manifest.Version);
        if (!Enum.IsDefined(manifest.Platform) || !Enum.IsDefined(manifest.Channel) || manifest.BuildNumber < 1 ||
            manifest.IterationNumber is < 1 or > 18 || manifest.GitCommitDateUtc == default || manifest.GitCommitDateUtc > DateTimeOffset.UtcNow ||
            !Regex.IsMatch(manifest.GitCommitHash ?? "", @"^(?:[0-9a-fA-F]{40}|[0-9a-fA-F]{64})$") ||
            !Regex.IsMatch(manifest.ExpectedSha256 ?? "", @"^[0-9a-fA-F]{64}$") ||
            string.IsNullOrWhiteSpace(manifest.ReleaseNotes) || manifest.ReleaseNotes.Length > 10000 || manifest.Compatibility is null || manifest.UpdatePolicy is null)
            throw new ArgumentException("Manifestet saknar giltig version, metadata, kompatibilitet eller checksumma.");
        if (manifest.Channel == ReleaseChannel.Stable && version.PreRelease is not null) throw new ArgumentException("Stable får inte innehålla prerelease.");
        _ = ApplicationVersion.Parse(manifest.Compatibility.MinimumServerVersion);
        if (manifest.Compatibility.MaximumServerVersion is not null && ApplicationVersion.Parse(manifest.Compatibility.MaximumServerVersion).CompareTo(ApplicationVersion.Parse(manifest.Compatibility.MinimumServerVersion)) < 0)
            throw new ArgumentException("Serverversionernas intervall är ogiltigt.");
    }

    private static void ValidatePackage(string path, ClientPlatform platform)
    {
        try
        {
            using var archive = ZipFile.OpenRead(path);
            var manifestName = platform == ClientPlatform.Android ? "AndroidManifest.xml" : "AppxManifest.xml";
            if (archive.Entries.Count is < 1 or > 100000 || archive.GetEntry(manifestName) is not { Length: > 0 })
                throw new ArgumentException("Paketet saknar plattformens manifest.");
            // Structural check only; configured verifier authenticates signature and embedded identity.
        }
        catch (InvalidDataException) { throw new ArgumentException("Installationsfilen är inte ett giltigt APK/MSIX-arkiv."); }
    }

    private async Task<FileStream> LockAsync(CancellationToken cancellationToken)
    {
        for (var i = 0; i < 100; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { return new(Path.Combine(root, "catalogue.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (i < 99) { await Task.Delay(50, cancellationToken); }
        }
        throw new IOException("Releasekatalogen är upptagen.");
    }
    private async Task SaveAsync(IReadOnlyList<ApplicationRelease> releases, CancellationToken cancellationToken)
    {
        var temporary = Path.Combine(root, $"catalogue-{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(output, releases, JsonOptions, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }
            File.Move(temporary, Path.Combine(root, "releases.json"), true);
        }
        finally { File.Delete(temporary); }
    }
}
