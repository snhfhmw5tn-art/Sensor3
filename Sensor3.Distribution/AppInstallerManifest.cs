using System.IO.Compression;
using System.Xml.Linq;
using Sensor3.Contracts;

namespace Sensor3.Distribution;

public static class AppInstallerManifest
{
    public static string Create(ApplicationRelease release, Stream package, string publicBaseUrl)
    {
        if (!Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var root) || root.Scheme != "https" || !string.IsNullOrEmpty(root.UserInfo))
            throw new InvalidDataException("Konfigurera portalens publika HTTPS-adress för App Installer.");
        if (release.Manifest.Platform != ClientPlatform.Windows) throw new ArgumentException("App Installer gäller Windows.");
        using var archive = new ZipArchive(package, ZipArchiveMode.Read, true);
        var entry = archive.GetEntry("AppxManifest.xml") ?? throw new InvalidDataException("Paketmanifest saknas.");
        if (entry.Length > 65536) throw new InvalidDataException("Paketmanifestet är för stort.");
        using var stream = entry.Open();
        var identity = XDocument.Load(stream).Root?.Elements().SingleOrDefault(x => x.Name.LocalName == "Identity") ?? throw new InvalidDataException("Paketidentitet saknas.");
        if ((string?)identity.Attribute("Name") != "se.qsys.sensor3" || (string?)identity.Attribute("Publisher") != "CN=Sensor3" || (string?)identity.Attribute("Version") != $"{release.Manifest.Version}.{release.Manifest.BuildNumber}")
            throw new InvalidDataException("Paketidentiteten stämmer inte.");
        XNamespace ns = "http://schemas.microsoft.com/appx/appinstaller/2018";
        var installerUrl = new Uri(root, $"/api/releases/appinstaller?channel={release.Manifest.Channel}");
        var main = new XElement(ns + "MainPackage", new XAttribute("Uri", new Uri(root, $"/api/releases/{release.Id}/download")));
        foreach (var name in new[] { "Name", "Publisher", "Version", "ProcessorArchitecture" })
            main.Add(new XAttribute(name, (string?)identity.Attribute(name) ?? throw new InvalidDataException("Paketattribut saknas.")));
        return new XDocument(new XElement(ns + "AppInstaller", new XAttribute("Uri", installerUrl), new XAttribute("Version", (string)identity.Attribute("Version")!), main,
            new XElement(ns + "UpdateSettings", new XElement(ns + "OnLaunch", new XAttribute("HoursBetweenUpdateChecks", "0"), new XAttribute("ShowPrompt", "true"), new XAttribute("UpdateBlocksActivation", "false"))))).ToString();
    }
}
