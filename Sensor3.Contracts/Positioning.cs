namespace Sensor3.Contracts;

public sealed record LocalPoint(double X, double Y);
public sealed record PositionSnapshot(LocalPoint? Position, double WalkingMeters, double RunningMeters, double? SpeedMetersPerSecond,
    double UncertaintyMeters, double Confidence, IReadOnlyList<LocalPoint> Path, string Detail);
public interface INavigationSession
{
    PositionSnapshot GetPosition();
    void SetStartPosition(double x, double y);
}
