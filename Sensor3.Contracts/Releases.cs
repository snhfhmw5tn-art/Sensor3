using System.Text.RegularExpressions;

namespace Sensor3.Contracts;

public enum ClientPlatform { Android, Windows }
public enum ReleaseChannel { Development, Beta, Stable }

public sealed record ApplicationVersion(int Major, int Minor, int Patch, string? PreRelease = null) : IComparable<ApplicationVersion>
{
    public static ApplicationVersion Parse(string value)
    {
        var match = Regex.Match(value ?? "", @"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (value?.Length > 128 || !match.Success || !int.TryParse(match.Groups[1].Value, out var major) ||
            !int.TryParse(match.Groups[2].Value, out var minor) || !int.TryParse(match.Groups[3].Value, out var patch))
            throw new ArgumentException("Version måste vara major.minor.patch med valfri prerelease.");
        var pre = match.Groups[4].Success ? match.Groups[4].Value : null;
        if (pre?.Split('.').Any(x => x.All(char.IsAsciiDigit) && x.Length > 1 && x[0] == '0') == true)
            throw new ArgumentException("Numerisk prerelease får inte ha inledande nollor.");
        return new(major, minor, patch, pre);
    }

    public int CompareTo(ApplicationVersion? other)
    {
        if (other is null) return 1;
        var comparison = Major.CompareTo(other.Major);
        if (comparison == 0) comparison = Minor.CompareTo(other.Minor);
        if (comparison == 0) comparison = Patch.CompareTo(other.Patch);
        if (comparison != 0) return comparison;
        if (PreRelease is null) return other.PreRelease is null ? 0 : 1;
        if (other.PreRelease is null) return -1;
        var left = PreRelease.Split('.');
        var right = other.PreRelease.Split('.');
        for (var i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            var leftNumber = left[i].All(char.IsAsciiDigit);
            var rightNumber = right[i].All(char.IsAsciiDigit);
            comparison = leftNumber && rightNumber
                ? (left[i].Length == right[i].Length ? string.CompareOrdinal(left[i], right[i]) : left[i].Length.CompareTo(right[i].Length))
                : leftNumber != rightNumber ? (leftNumber ? -1 : 1) : string.CompareOrdinal(left[i], right[i]);
            if (comparison != 0) return comparison;
        }
        return left.Length.CompareTo(right.Length);
    }
    public override string ToString() => $"{Major}.{Minor}.{Patch}{(PreRelease is null ? "" : "-" + PreRelease)}";
}

public sealed record ClientCompatibility(string MinimumServerVersion, string? MaximumServerVersion = null)
{
    public bool Supports(string serverVersion) => ApplicationVersion.Parse(serverVersion).CompareTo(ApplicationVersion.Parse(MinimumServerVersion)) >= 0 &&
        (MaximumServerVersion is null || ApplicationVersion.Parse(serverVersion).CompareTo(ApplicationVersion.Parse(MaximumServerVersion)) <= 0);
}
public sealed record UpdatePolicy(bool Mandatory = false);
public sealed record ReleaseArtifact(string FileName, long FileSize, string Sha256, string ContentType);
public sealed record ReleaseManifest
{
    public ClientPlatform Platform { get; init; }
    public ReleaseChannel Channel { get; init; }
    public string Version { get; init; } = "";
    public long BuildNumber { get; init; }
    public string GitCommitHash { get; init; } = "";
    public DateTimeOffset GitCommitDateUtc { get; init; }
    public int IterationNumber { get; init; }
    public string ReleaseNotes { get; init; } = "";
    public string ExpectedSha256 { get; init; } = "";
    public ClientCompatibility Compatibility { get; init; } = new("0.2.0");
    public UpdatePolicy UpdatePolicy { get; init; } = new();
}
public sealed record ApplicationRelease(Guid Id, ReleaseManifest Manifest, ReleaseArtifact Artifact, DateTimeOffset PublishedAtUtc, bool IsRevoked = false);
public sealed record UpdateCheckResult(string Status, bool UpdateAvailable, bool Mandatory, bool CurrentReleaseRevoked, ApplicationRelease? Release);
