namespace Sensor3.Contracts;

public enum ActivityKind { Unknown, Stationary, Walking, Running, Forklift }
public sealed record ActivityContext(bool DeclaredForklift = false, double? GpsSpeedMetersPerSecond = null, bool TrustedGps = false);
public sealed record ActivityEstimate(ActivityKind Kind, ActivityKind Candidate, double Confidence, bool Stable, double Seconds, string Explanation);
public interface IActivityClassifier
{
    ActivityEstimate Update(GaitFeatures? features, double seconds, ActivityContext context);
    void Reset();
}
public interface IActivitySession
{
    ActivityEstimate GetActivity();
    GaitFeatures? GetActivityFeatures();
    bool IsForkliftDeclared { get; }
    void SetDeclaredForklift(bool declared);
}
