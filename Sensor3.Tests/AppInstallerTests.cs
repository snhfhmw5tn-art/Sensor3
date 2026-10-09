using System.IO.Compression;
using System.Xml.Linq;
using Sensor3.Contracts;
using Sensor3.Distribution;

namespace Sensor3.Tests;

[TestClass]
public sealed class AppInstallerTests
{
    private static MemoryStream Package(string name = "se.qsys.sensor3")
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        using (var writer = new StreamWriter(zip.CreateEntry("AppxManifest.xml").Open()))
            writer.Write($"<Package><Identity Name='{name}' Publisher='CN=Sensor3' Version='0.3.0.3' ProcessorArchitecture='x64'/></Package>");
        stream.Position = 0;
        return stream;
    }
    private static ApplicationRelease Release() => new(Guid.NewGuid(), new ReleaseManifest { Platform = ClientPlatform.Windows, Channel = ReleaseChannel.Stable, Version = "0.3.0", BuildNumber = 3 }, new("Sensor3.msix", 1, "", "application/msix"), DateTimeOffset.UtcNow);
    [TestMethod]
    public void TestThat_appinstaller_uses_stable_channel_endpoint_and_launch_prompt()
    {
        using var package = Package();
        var document = XDocument.Parse(AppInstallerManifest.Create(Release(), package, "https://updates.example"));
        Assert.AreEqual("https://updates.example/api/releases/Sensor3.appinstaller?channel=Stable", (string?)document.Root!.Attribute("Uri"));
        var launch = document.Descendants().Single(x => x.Name.LocalName == "OnLaunch");
        Assert.AreEqual("0", (string?)launch.Attribute("HoursBetweenUpdateChecks"));
        Assert.AreEqual("true", (string?)launch.Attribute("ShowPrompt"));
        Assert.AreEqual("false", (string?)launch.Attribute("UpdateBlocksActivation"));
        Assert.IsFalse(document.Descendants().Any(x => x.Name.LocalName == "AutomaticBackgroundTask"));
    }
    [TestMethod]
    public void TestThat_http_appinstaller_url_is_rejected()
    {
        using var package = Package();
        Assert.ThrowsExactly<InvalidDataException>(() => AppInstallerManifest.Create(Release(), package, "http://updates.example"));
    }
    [TestMethod]
    public void TestThat_wrong_package_identity_is_rejected()
    {
        using var package = Package("other.app");
        Assert.ThrowsExactly<InvalidDataException>(() => AppInstallerManifest.Create(Release(), package, "https://updates.example"));
    }
    [TestMethod]
    public async Task TestThat_publication_requires_configured_package_signer()
    {
        var verifier = new PackageVerifier(new());
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => verifier.VerifyAsync("unused", new() { Platform = ClientPlatform.Android }, default));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => verifier.VerifyAsync("unused", new() { Platform = ClientPlatform.Windows }, default));
    }
}
