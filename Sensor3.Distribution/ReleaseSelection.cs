using Sensor3.Contracts;

namespace Sensor3.Distribution;

public static class ReleaseSelection
{
    public static ApplicationRelease? Latest(IEnumerable<ApplicationRelease> releases, ClientPlatform platform, ReleaseChannel channel, string serverVersion) =>
        releases.Where(x => !x.IsRevoked && x.Manifest.Platform == platform && x.Manifest.Channel == channel && x.Manifest.Compatibility.Supports(serverVersion))
            .OrderByDescending(x => ApplicationVersion.Parse(x.Manifest.Version)).ThenByDescending(x => x.Manifest.BuildNumber).FirstOrDefault();

    public static UpdateCheckResult Check(IEnumerable<ApplicationRelease> releases, ClientPlatform platform, ReleaseChannel channel, string version, long build, string serverVersion)
    {
        var current = ApplicationVersion.Parse(version);
        if (build < 1) throw new ArgumentException("Buildnummer måste vara positivt.");
        var entries = releases.ToArray();
        var revoked = entries.Any(x => x.IsRevoked && x.Manifest.Platform == platform && x.Manifest.Channel == channel && x.Manifest.Version == version && x.Manifest.BuildNumber == build);
        var latest = Latest(entries, platform, channel, serverVersion);
        var update = latest is not null && (ApplicationVersion.Parse(latest.Manifest.Version).CompareTo(current) > 0 ||
            ApplicationVersion.Parse(latest.Manifest.Version).CompareTo(current) == 0 && latest.Manifest.BuildNumber > build);
        // A mandatory intermediate release must not become optional when superseded.
        var mandatory = update && (revoked || entries.Any(x => !x.IsRevoked && x.Manifest.Platform == platform && x.Manifest.Channel == channel &&
            x.Manifest.Compatibility.Supports(serverVersion) && x.Manifest.UpdatePolicy.Mandatory &&
            (ApplicationVersion.Parse(x.Manifest.Version).CompareTo(current) > 0 || x.Manifest.Version == version && x.Manifest.BuildNumber > build)));
        return new(latest is null ? "NoCompatibleRelease" : update ? "UpdateAvailable" : "UpToDate", update, mandatory, revoked, latest);
    }
}
