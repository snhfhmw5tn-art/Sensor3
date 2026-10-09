namespace Sensor3.Contracts;

public enum CarryingKind { Unknown, Viewing, HeldStable, HandSwinging, VerticalSwinging, HandRotating, Scanning, Pocket, PickingUp, PuttingAway, DeviceStationary, Mounted }
public sealed record CarryingContext(double? ProximityMeters = null, double? LightLux = null, CarryingKind Declared = CarryingKind.Unknown);
public sealed record CarryingEstimate(CarryingKind Kind, bool Transition, double Confidence, IReadOnlyDictionary<CarryingKind, double> Probabilities,
    double CrossWindowAccelerationChange, double WithinWindowAccelerationRange, string Explanation);
public interface ICarryingClassifier
{
    CarryingEstimate Update(DeviceMotion motion, DeviceOrientation? orientation, ActivityEstimate activity, CarryingContext context);
    void Reset();
}
public interface ICarryingSession
{
    CarryingEstimate GetCarrying();
    CarryingKind DeclaredCarrying { get; }
    void SetDeclaredCarrying(CarryingKind kind);
}
