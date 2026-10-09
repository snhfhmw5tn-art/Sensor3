using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Sensor3.Contracts;

namespace Sensor3.Distribution;

public interface IReleasePackageVerifier
{
    Task VerifyAsync(string path, ReleaseManifest manifest, CancellationToken cancellationToken);
}
public sealed class PackageVerifier(DistributionOptions options) : IReleasePackageVerifier
{
    public async Task VerifyAsync(string path, ReleaseManifest manifest, CancellationToken cancellationToken)
    {
        if (manifest.Platform == ClientPlatform.Android)
        {
            if (options.AndroidCertificateSha256.Length != 64 || !options.AndroidCertificateSha256.All(char.IsAsciiHexDigit))
                throw new InvalidDataException("Konfigurera betrodd Android-signatur innan publicering.");
            var signature = await RunAsync(options.AndroidApkSignerPath, ["verify", "--print-certs", path], cancellationToken);
            var certs = Regex.Matches(signature, @"Signer #\d+ certificate SHA-256 digest: ([0-9a-fA-F]{64})");
            if (certs.Count != 1 || !certs[0].Groups[1].Value.Equals(options.AndroidCertificateSha256, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("APK har fel signeringscertifikat.");
            var metadata = await RunAsync(options.AndroidAaptPath, ["dump", "badging", path], cancellationToken);
            var package = Regex.Match(metadata, @"package: name='([^']+)' versionCode='([^']+)' versionName='([^']+)'");
            if (!package.Success || package.Groups[1].Value != "se.qsys.sensor3" || package.Groups[2].Value != manifest.BuildNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) || package.Groups[3].Value != manifest.Version)
                throw new ArgumentException("APK-identitet/version stämmer inte med releasemanifestet.");
        }
        else
        {
            await RunAsync(options.WindowsSignToolPath, ["verify", "/pa", path], cancellationToken);
            using var archive = ZipFile.OpenRead(path);
            var entry = archive.GetEntry("AppxManifest.xml") ?? throw new ArgumentException("MSIX-manifest saknas.");
            if (entry.Length > 65536) throw new ArgumentException("MSIX-manifestet är för stort.");
            using var stream = entry.Open();
            var document = XDocument.Load(stream);
            var identity = document.Root?.Elements().SingleOrDefault(x => x.Name.LocalName == "Identity");
            if ((string?)identity?.Attribute("Name") != "se.qsys.sensor3" || (string?)identity?.Attribute("Publisher") != "CN=Sensor3" ||
                (string?)identity?.Attribute("Version") != $"{manifest.Version}.{manifest.BuildNumber}")
                throw new ArgumentException("MSIX-identitet/version stämmer inte med releasemanifestet.");
        }
    }
    private async Task<string> RunAsync(string tool, string[] arguments, CancellationToken cancellationToken)
    {
        if (!Path.IsPathFullyQualified(tool) || !File.Exists(tool)) throw new InvalidDataException("Serverns signaturverktyg är inte konfigurerat.");
        var info = new ProcessStartInfo { FileName = tool, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        if (tool.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
        {
            if (!Path.IsPathFullyQualified(options.JavaPath) || !File.Exists(options.JavaPath)) throw new InvalidDataException("Java för APK-verifiering saknas.");
            info.FileName = options.JavaPath;
            info.ArgumentList.Add("-jar");
            info.ArgumentList.Add(tool);
        }
        if (tool.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) || tool.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Verifieringsverktyget måste vara en körbar fil eller apksigner.jar.");
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new IOException("Verifieringsverktyget kunde inte startas.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            var result = await output;
            await error;
            if (process.ExitCode != 0) throw new ArgumentException("Installationspaketets signatur kunde inte verifieras.");
            return result;
        }
        finally { if (!process.HasExited) process.Kill(true); }
    }
}
