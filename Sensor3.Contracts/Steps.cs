namespace Sensor3.Contracts;

public sealed record GaitSample(double Seconds, double VerticalAcceleration, double HorizontalX, double HorizontalY, double GyroMagnitudeRadians, bool OrientationReliable);
public sealed record GaitFeatures(double Rms, double Variance, double HorizontalEnergy, double GyroRmsRadians, double Amplitude, double CadenceHz, double Periodicity, double SampleRateHz, bool OrientationReliable);
public sealed record StepEvent(double Seconds, double IntervalSeconds, double Amplitude, bool Running, double LengthMeters, double Confidence);
public sealed record StepSnapshot(long Total, long Walking, long Running, double CadenceHz, double DistanceMeters, double? LastStepLengthMeters, double Confidence, double? NativeTotal, string Detail, string Source = "Unknown");
public interface IStepSession
{
    void Configure(IReadOnlyList<SensorDescriptor> selected);
    StepSnapshot GetSnapshot();
    bool UsesSensorForPositioning(string sensorId);
    void Reset();
}
