namespace Sensor3.Contracts;

public sealed record BuildInfo
{
    public string ApplicationName { get; init; } = "Sensor 3";
    public string ApplicationVersion { get; init; } = "Unknown";
    public string BuildNumber { get; init; } = "Unknown";
    public string GitCommitHash { get; init; } = "Unknown";
    public string GitCommitShortHash { get; init; } = "Unknown";
    public DateTimeOffset? GitCommitDateUtc { get; init; }
    public DateTimeOffset? BuildDateUtc { get; init; }
    public string GitBranch { get; init; } = "Unknown";
    public bool IsDirtyBuild { get; init; }
    public string TargetPlatform { get; init; } = "Unknown";
    public string ReleaseChannel { get; init; } = "Development";
    public int LatestImplementedIteration { get; init; }
    public int LatestVerifiedIteration { get; init; }
    public int LatestPublishedIteration { get; init; }
    public string BuildIdentifier { get; init; } = "Unknown";
}
