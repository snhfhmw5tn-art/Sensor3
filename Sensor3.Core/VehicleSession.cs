using Sensor3.Contracts;
using Sensor3.Positioning;
namespace Sensor3.Core;
public sealed class VehicleSession : IForkliftMotionEstimator, IDisposable
{
    private readonly object gate = new();
    private readonly NativeObservationBus bus;
    private readonly IActivitySession activity;
    private readonly ISensorFusionSession fusion;
    private readonly ForkliftMotionEstimator estimator = new();
    public VehicleSession(NativeObservationBus bus, IActivitySession activity, ISensorFusionSession fusion)
    { this.bus = bus; this.activity = activity; this.fusion = fusion; bus.LocationReceived += Receive; }
    private void Receive(LocationObservation fix) => Update(fix, activity.IsForkliftDeclared, fusion.GetFusionSnapshot().Motion);
    public void SetGpsOrigin(double latitude, double longitude) { lock (gate) estimator.SetGpsOrigin(latitude, longitude); }
    public void Update(LocationObservation fix, bool declaredForklift, DeviceMotion? motion) { lock (gate) estimator.Update(fix, declaredForklift, motion); }
    public VehicleSnapshot GetSnapshot() { lock (gate) return estimator.GetSnapshot(); }
    public void Dispose() => bus.LocationReceived -= Receive;
}
