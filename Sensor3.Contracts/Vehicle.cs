namespace Sensor3.Contracts;
public sealed record VehicleSnapshot(LocalPoint? Position, double DistanceMeters, double? SpeedMetersPerSecond, double? CourseRadians,
    double? AccuracyMeters, double Confidence, string Detail);
public interface IForkliftMotionEstimator
{
    void SetGpsOrigin(double latitude, double longitude);
    void Update(LocationObservation fix, bool declaredForklift, DeviceMotion? motion);
    VehicleSnapshot GetSnapshot();
}
// The bus carries observations actually returned by native APIs; no hardware acquisition occurs here.
public sealed class NativeObservationBus
{
    public event Action<LocationObservation>? LocationReceived;
    public event Action<IReadOnlyList<WifiObservation>>? WifiReceived;
    public event Action<IReadOnlyList<BluetoothObservation>>? BluetoothReceived;
    public void Publish(LocationObservation value) => LocationReceived?.Invoke(value);
    public void Publish(IReadOnlyList<WifiObservation> values) => WifiReceived?.Invoke(values.ToArray());
    public void Publish(IReadOnlyList<BluetoothObservation> values) => BluetoothReceived?.Invoke(values.ToArray());
}
