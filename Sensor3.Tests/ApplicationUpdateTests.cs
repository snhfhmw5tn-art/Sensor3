using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Sensor3.Contracts;
using Sensor3.Core;

namespace Sensor3.Tests;

[TestClass]
public sealed class ApplicationUpdateTests
{
    private sealed class Installer : IUpdateInstaller
    {
        public int Calls;
        public Task StartAsync(string packagePath, CancellationToken cancellationToken) { Assert.IsTrue(File.Exists(packagePath)); Calls++; return Task.CompletedTask; }
    }
    private sealed class Handler(RSA key, ApplicationRelease release, byte[] package, string scenario) : HttpMessageHandler
    {
        private int checks;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("download"))
            {
                if (scenario == "network") throw new HttpRequestException("Disconnected");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(scenario == "corrupt" ? [9, 9, 9] : scenario == "short" ? [1] : package) });
            }
            var nonce = request.RequestUri.Query.Split('&').Single(x => x.StartsWith("nonce=", StringComparison.Ordinal))[6..];
            var selected = scenario == "channel" ? release with { Manifest = release.Manifest with { Channel = ReleaseChannel.Beta } } : release;
            var revoked = scenario == "revoked" && ++checks > 1;
            if (scenario == "downgrade") selected = release with { Manifest = release.Manifest with { Version = "0.1.0" } };
            if (scenario == "platform") selected = release with { Manifest = release.Manifest with { Platform = ClientPlatform.Windows } };
            var payload = JsonSerializer.SerializeToUtf8Bytes(new AuthenticatedUpdate(scenario == "replay" ? "wrong" : nonce,
                scenario == "expired" ? DateTimeOffset.UtcNow.AddMinutes(-1) : DateTimeOffset.UtcNow.AddMinutes(5),
                revoked ? new UpdateCheckResult("NoCompatibleRelease", false, false, true, null) : new UpdateCheckResult("UpdateAvailable", true, false, false, selected)), ApplicationUpdateService.JsonOptions);
            var signature = key.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
            if (scenario == "signature") signature[0] ^= 1;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new SignedUpdateEnvelope(Convert.ToBase64String(payload), Convert.ToBase64String(signature)), options: ApplicationUpdateService.JsonOptions) });
        }
    }
    private sealed class Fixture : IDisposable
    {
        public readonly RSA Key = RSA.Create(2048);
        public readonly Installer Installer = new();
        public readonly UpdateSessionGuard Guard = new();
        public readonly string Root = Path.Combine(Path.GetTempPath(), "sensor3-update-tests-" + Guid.NewGuid());
        public ApplicationUpdateService Service { get; }
        private readonly HttpClient http;
        public Fixture(string scenario = "valid", string scheme = "https")
        {
            byte[] bytes = [1, 2, 3];
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            var release = new ApplicationRelease(Guid.NewGuid(), new ReleaseManifest { Platform = ClientPlatform.Android, Channel = ReleaseChannel.Stable, Version = "0.3.0", BuildNumber = 3, ExpectedSha256 = hash },
                new ReleaseArtifact("Sensor3.apk", bytes.Length, hash, "application/vnd.android.package-archive"), DateTimeOffset.UtcNow);
            http = new HttpClient(new Handler(Key, release, bytes, scenario));
            Service = new(http, new ApplicationUpdateOptions { ServerUrl = scheme + "://updates.example", ManifestPublicKeyPem = Key.ExportSubjectPublicKeyInfoPem() }, new("0.2.0", 2, ClientPlatform.Android), Installer, Guard, Root);
        }
        public void Dispose() { http.Dispose(); Key.Dispose(); if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
    [TestMethod]
    public async Task TestThat_authenticated_update_installs_verified_file()
    {
        using var f = new Fixture();
        await f.Service.CheckAsync();
        await f.Service.InstallAsync();
        Assert.AreEqual(1, f.Installer.Calls);
        Assert.IsTrue(f.Service.Available!.UpdateAvailable);
    }
    [TestMethod]
    [DataRow("signature", DisplayName = "Fel signatur")]
    [DataRow("channel", DisplayName = "Fel kanal")]
    [DataRow("platform", DisplayName = "Fel plattform")]
    [DataRow("downgrade", DisplayName = "Nedgradering")]
    [DataRow("replay", DisplayName = "Återspelat manifest")]
    [DataRow("expired", DisplayName = "Utgånget manifest")]
    public async Task TestThat_invalid_manifest_is_rejected(string scenario)
    {
        using var f = new Fixture(scenario);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => f.Service.CheckAsync());
        Assert.IsNull(f.Service.Available);
        Assert.AreEqual(0, f.Installer.Calls);
    }
    [TestMethod]
    [DataRow("corrupt", DisplayName = "Korrupt APK")]
    [DataRow("short", DisplayName = "Ofullständig APK")]
    public async Task TestThat_bad_download_is_deleted(string scenario)
    {
        using var f = new Fixture(scenario);
        await f.Service.CheckAsync();
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => f.Service.InstallAsync());
        Assert.AreEqual(0, f.Installer.Calls);
        Assert.HasCount(0, Directory.GetFiles(f.Root));
    }
    [TestMethod]
    public async Task TestThat_network_interruption_does_not_install()
    {
        using var f = new Fixture("network");
        await f.Service.CheckAsync();
        await Assert.ThrowsExactlyAsync<HttpRequestException>(() => f.Service.InstallAsync());
        Assert.AreEqual(0, f.Installer.Calls);
        Assert.HasCount(0, Directory.GetFiles(f.Root));
    }
    [TestMethod]
    public async Task TestThat_active_session_blocks_installation()
    {
        using var f = new Fixture();
        await f.Service.CheckAsync();
        using var session = f.Guard.BeginSession();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => f.Service.InstallAsync());
        Assert.AreEqual(0, f.Installer.Calls);
    }
    [TestMethod]
    public void TestThat_session_cannot_start_during_installation()
    {
        var guard = new UpdateSessionGuard();
        using (guard.AcquireInstallation()) Assert.ThrowsExactly<InvalidOperationException>(() => guard.BeginSession());
        using var session = guard.BeginSession();
        Assert.IsTrue(guard.IsSessionActive);
    }
    [TestMethod]
    public async Task TestThat_http_configuration_is_rejected()
    {
        using var f = new Fixture(scheme: "http");
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => f.Service.CheckAsync());
    }
    [TestMethod]
    public async Task TestThat_install_requires_prior_authenticated_check()
    {
        using var f = new Fixture();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => f.Service.InstallAsync());
    }
    [TestMethod]
    public async Task TestThat_revocation_during_download_blocks_installer()
    {
        using var f = new Fixture("revoked");
        await f.Service.CheckAsync();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => f.Service.InstallAsync());
        Assert.AreEqual(0, f.Installer.Calls);
        Assert.HasCount(0, Directory.GetFiles(f.Root));
    }
    [TestMethod]
    public async Task TestThat_cancelled_download_does_not_install()
    {
        using var f = new Fixture();
        await f.Service.CheckAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => f.Service.InstallAsync(cancellation.Token));
        Assert.AreEqual(0, f.Installer.Calls);
    }
}
