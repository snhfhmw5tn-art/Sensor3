namespace Sensor3.Contracts;

public sealed record HumanHeading(double? Radians, double Confidence, double UncertaintyRadians, string Method, string Detail);
public sealed record HeadingComparison(double? PcaRadians, double? GyroOnlyRadians, double? AdaptiveRadians, double Anisotropy, double PhaseCorrelation);
public interface IDeviceOrientationEstimator { DeviceOrientation? GetDeviceOrientation(); }
public interface IHumanHeadingEstimator
{
    HumanHeading Update(DeviceMotion motion, DeviceOrientation? orientation, ActivityEstimate activity, CarryingEstimate carrying, double? cadenceHz = null);
    void SetKnownStartHeading(double radians);
    HumanHeading GetHeading();
    HeadingComparison GetComparison();
    void ResetEvidence();
}
public interface IHumanHeadingSession
{
    HumanHeading GetHeading();
    HeadingComparison GetHeadingComparison();
    void SetKnownStartHeading(double radians);
}
