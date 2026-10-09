using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Sensor3.Contracts;
using Sensor3.Distribution;

namespace Sensor3.Tests;

[TestClass]
public sealed class DistributionTests
{
    private string root = "";
    [TestInitialize] public void Initialize() => root = Path.Combine(Path.GetTempPath(), "sensor3-tests", Guid.NewGuid().ToString("N"));
    [TestCleanup] public void Cleanup()
    {
        var expected = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "sensor3-tests")) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(root).StartsWith(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unsafe test cleanup target");
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    [TestMethod]
    [DataRow("1.10.0", "1.9.0", 1, DisplayName = "Numeriska versioner")]
    [DataRow("1.0.0", "1.0.0-beta", 1, DisplayName = "Release efter prerelease")]
    [DataRow("1.0.0-beta.11", "1.0.0-beta.2", 1, DisplayName = "Numerisk prerelease")]
    [DataRow("1.0.0-alpha", "1.0.0-beta", -1, DisplayName = "Prerelease-sortering")]
    [DataRow("1.0.0", "1.0.0", 0, DisplayName = "Lika versioner")]
    public void TestThat_versions_follow_semantic_precedence(string left, string right, int expected) =>
        Assert.AreEqual(expected, Math.Sign(ApplicationVersion.Parse(left).CompareTo(ApplicationVersion.Parse(right))));

    [TestMethod]
    [DataRow("1.0", DisplayName = "Saknat patchnummer")]
    [DataRow("01.0.0", DisplayName = "Inledande nollor")]
    [DataRow("1.0.0-beta.01", DisplayName = "Prerelease med nollor")]
    [DataRow("../../x", DisplayName = "Ogiltig versionssträng")]
    public void TestThat_invalid_versions_are_rejected(string version) => Assert.ThrowsExactly<ArgumentException>(() => ApplicationVersion.Parse(version));

    [TestMethod]
    public void TestThat_latest_respects_platform_channel_compatibility_and_revocation()
    {
        var valid = Release("1.1.0", 2);
        var entries = new[] { valid, Release("9.0.0", 3) with { IsRevoked = true },
            Release("8.0.0", 4) with { Manifest = Manifest("8.0.0", 4) with { Platform = ClientPlatform.Windows } },
            Release("7.0.0", 5) with { Manifest = Manifest("7.0.0", 5) with { Channel = ReleaseChannel.Beta } },
            Release("6.0.0", 6) with { Manifest = Manifest("6.0.0", 6) with { Compatibility = new("5.0.0") } } };
        Assert.AreEqual(valid.Id, ReleaseSelection.Latest(entries, ClientPlatform.Android, ReleaseChannel.Development, "0.2.0")?.Id);
        Assert.IsNull(ReleaseSelection.Latest(entries, ClientPlatform.Windows, ReleaseChannel.Stable, "0.2.0"));
    }
    [TestMethod]
    public void TestThat_build_number_breaks_equal_version_ties() => Assert.AreEqual(3L,
        ReleaseSelection.Latest([Release("1.0.0", 2), Release("1.0.0", 3)], ClientPlatform.Android, ReleaseChannel.Development, "0.2.0")?.Manifest.BuildNumber);
    [TestMethod]
    public void TestThat_mandatory_intermediate_update_is_preserved()
    {
        var required = Release("1.1.0", 2) with { Manifest = Manifest("1.1.0", 2) with { UpdatePolicy = new(true) } };
        var result = ReleaseSelection.Check([required, Release("1.2.0", 3)], ClientPlatform.Android, ReleaseChannel.Development, "1.0.0", 1, "0.2.0");
        Assert.IsTrue(result.UpdateAvailable); Assert.IsTrue(result.Mandatory); Assert.AreEqual("1.2.0", result.Release?.Manifest.Version);
    }
    [TestMethod]
    public void TestThat_newer_client_is_not_downgraded() => Assert.IsFalse(
        ReleaseSelection.Check([Release("1.0.0", 1)], ClientPlatform.Android, ReleaseChannel.Development, "2.0.0", 2, "0.2.0").UpdateAvailable);
    [TestMethod]
    public void TestThat_revoked_client_and_missing_compatible_release_are_reported()
    {
        var result = ReleaseSelection.Check([Release("1.0.0", 1) with { IsRevoked = true }], ClientPlatform.Android, ReleaseChannel.Development, "1.0.0", 1, "0.2.0");
        Assert.IsTrue(result.CurrentReleaseRevoked); Assert.AreEqual("NoCompatibleRelease", result.Status); Assert.IsFalse(result.UpdateAvailable);
    }
    [TestMethod]
    public void TestThat_server_version_bounds_are_inclusive()
    {
        var compatibility = new ClientCompatibility("1.0.0", "2.0.0");
        Assert.IsTrue(compatibility.Supports("1.0.0")); Assert.IsTrue(compatibility.Supports("2.0.0")); Assert.IsFalse(compatibility.Supports("2.0.1"));
    }
    [TestMethod]
    public async Task TestThat_publication_persists_checksum_and_can_be_revoked()
    {
        var bytes = Package(); var store = Store();
        var release = await store.PublishAsync(ManifestFor(bytes), "sensor.apk", new MemoryStream(bytes));
        Assert.AreEqual(bytes.LongLength, release.Artifact.FileSize);
        Assert.AreEqual(Convert.ToHexStringLower(SHA256.HashData(bytes)), release.Artifact.Sha256);
        Assert.AreEqual(TimeSpan.Zero, release.PublishedAtUtc.Offset);
        Assert.HasCount(1, await Store().ListAsync());
        var download = await store.OpenDownloadAsync(release.Id);
        Assert.IsNotNull(download); await download.Value.Content.DisposeAsync();
        Assert.IsTrue(await store.RevokeAsync(release.Id));
        Assert.IsNull(await Store().OpenDownloadAsync(release.Id));
    }
    [TestMethod]
    [DataRow("../evil.apk", DisplayName = "Path traversal")]
    [DataRow("folder\\evil.apk", DisplayName = "Windows traversal")]
    [DataRow("evil.exe", DisplayName = "Körbar serverfil")]
    [DataRow("evil.msix", DisplayName = "Fel plattform")]
    public async Task TestThat_unsafe_file_names_are_rejected(string name)
    {
        var bytes = Package();
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => Store().PublishAsync(ManifestFor(bytes), name, new MemoryStream(bytes)));
    }
    [TestMethod]
    public async Task TestThat_wrong_checksum_and_size_limit_leave_no_release()
    {
        var bytes = Package(); var store = Store();
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.PublishAsync(ManifestFor(bytes) with { ExpectedSha256 = new('0', 64) }, "sensor.apk", new MemoryStream(bytes)));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => Store(1).PublishAsync(ManifestFor(bytes), "sensor.apk", new MemoryStream(bytes)));
        Assert.HasCount(0, await store.ListAsync()); Assert.IsEmpty(Directory.GetFiles(root, "*.package")); Assert.IsEmpty(Directory.GetFiles(root, "*.upload"));
    }
    [TestMethod]
    public async Task TestThat_package_content_is_validated_beyond_extension()
    {
        var bytes = "not an APK"u8.ToArray();
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => Store().PublishAsync(ManifestFor(bytes), "sensor.apk", new MemoryStream(bytes)));
        bytes = Package("AppxManifest.xml");
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => Store().PublishAsync(ManifestFor(bytes), "sensor.apk", new MemoryStream(bytes)));
    }
    [TestMethod]
    public async Task TestThat_windows_package_has_its_own_type()
    {
        var bytes = Package("AppxManifest.xml");
        var release = await Store().PublishAsync(ManifestFor(bytes) with { Platform = ClientPlatform.Windows }, "sensor.msix", new MemoryStream(bytes));
        Assert.AreEqual("application/msix", release.Artifact.ContentType);
    }
    [TestMethod]
    public async Task TestThat_tampered_download_is_blocked()
    {
        var bytes = Package(); var store = Store();
        var release = await store.PublishAsync(ManifestFor(bytes), "sensor.apk", new MemoryStream(bytes));
        await File.WriteAllTextAsync(Path.Combine(root, $"{release.Id:N}.package"), "corrupt");
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => store.OpenDownloadAsync(release.Id));
    }
    [TestMethod]
    public async Task TestThat_duplicate_and_non_monotonic_builds_are_rejected()
    {
        var bytes = Package(); var store = Store();
        await store.PublishAsync(ManifestFor(bytes), "sensor.apk", new MemoryStream(bytes));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.PublishAsync(ManifestFor(bytes), "sensor.apk", new MemoryStream(bytes)));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.PublishAsync(ManifestFor(bytes) with { Channel = ReleaseChannel.Beta }, "sensor.apk", new MemoryStream(bytes)));
    }
    [TestMethod]
    public void TestThat_invalid_metadata_and_stable_prerelease_are_rejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() => ReleaseStore.ValidateManifest(Manifest() with { GitCommitHash = "Unknown" }));
        Assert.ThrowsExactly<ArgumentException>(() => ReleaseStore.ValidateManifest(Manifest("1.0.0-beta") with { Channel = ReleaseChannel.Stable }));
        Assert.ThrowsExactly<ArgumentException>(() => ReleaseStore.ValidateManifest(Manifest() with { Compatibility = new("2.0.0", "1.0.0") }));
    }
    [TestMethod]
    public void TestThat_admin_password_hashes_are_salted_and_fail_closed()
    {
        const string password = "local-test-password-only";
        var hash = AdminCredentials.Hash(password);
        Assert.IsTrue(AdminCredentials.Verify(password, hash)); Assert.IsFalse(AdminCredentials.Verify("wrong", hash));
        Assert.IsFalse(AdminCredentials.Verify(password, "")); Assert.AreNotEqual(hash, AdminCredentials.Hash(password));
        Assert.ThrowsExactly<ArgumentException>(() => AdminCredentials.Hash("short"));
    }
    [TestMethod]
    public async Task TestThat_corrupt_catalogue_is_not_treated_as_empty()
    {
        var store = Store();
        await File.WriteAllTextAsync(Path.Combine(root, "releases.json"), "{broken");
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => store.ListAsync());
    }
    [TestMethod]
    public async Task TestThat_cancelled_publication_leaves_no_partial_package()
    {
        var store = Store(); var bytes = Package();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => store.PublishAsync(ManifestFor(bytes), "sensor.apk", new MemoryStream(bytes), cancellation.Token));
        Assert.IsEmpty(Directory.GetFiles(root, "*.upload")); Assert.HasCount(0, await store.ListAsync());
    }
    [TestMethod]
    public async Task TestThat_concurrent_duplicate_publications_preserve_single_release()
    {
        var bytes = Package(); var store = Store();
        async Task<bool> Publish()
        {
            try { await store.PublishAsync(ManifestFor(bytes), "sensor.apk", new MemoryStream(bytes)); return true; }
            catch (ArgumentException) { return false; }
        }
        var results = await Task.WhenAll(Publish(), Publish());
        Assert.AreEqual(1, results.Count(x => x)); Assert.HasCount(1, await store.ListAsync());
    }
    private ReleaseStore Store(long maximum = 1024 * 1024) => new(new() { StorageRoot = root, MaximumArtifactBytes = maximum }, NullLogger<ReleaseStore>.Instance);
    private static ReleaseManifest Manifest(string version = "1.0.0", long build = 1) => new() { Platform = ClientPlatform.Android, Channel = ReleaseChannel.Development,
        Version = version, BuildNumber = build, GitCommitHash = new('a', 40), GitCommitDateUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
        IterationNumber = 1, ExpectedSha256 = new('a', 64), ReleaseNotes = "Synthetic test fixture, not a release", Compatibility = new("0.2.0") };
    private static ReleaseManifest ManifestFor(byte[] bytes) => Manifest() with { ExpectedSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)) };
    private static ApplicationRelease Release(string version, long build) => new(Guid.NewGuid(), Manifest(version, build), new("test.apk", 1, new('a', 64), "application/vnd.android.package-archive"), DateTimeOffset.UtcNow);
    private static byte[] Package(string manifest = "AndroidManifest.xml")
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        using (var writer = new StreamWriter(zip.CreateEntry(manifest).Open())) writer.Write("Synthetic test fixture; not installable");
        return stream.ToArray();
    }
}
