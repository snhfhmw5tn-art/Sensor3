namespace Sensor3.Contracts;

public enum IterationStatus { NotStarted, InProgress, Implemented, Verified, Blocked, Released }

public sealed record IterationInfo(int Number, string Name, string Description, IterationStatus Status,
    string LatestCommit, DateTimeOffset? CommitDateUtc, DateTimeOffset? VerifiedAtUtc, string TestResult, string[] Blockers);
