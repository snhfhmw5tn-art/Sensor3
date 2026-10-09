using Sensor3.Contracts;
using Sensor3.Positioning;
namespace Sensor3.Core;
public sealed class VehicleSession : IForkliftMotionEstimator, IAnalysisContextSource, IDisposable
{
    public event Action<AnalysisContext>? ContextChanged;
    public AnalysisContext GetAnalysisContext() => new(GpsOrigin: GetGpsOrigin());
    private readonly object gate = new();
    private readonly NativeObservationBus bus;
    private readonly IActivitySession activity;
    private readonly ISensorFusionSession fusion;
    private readonly ForkliftMotionEstimator estimator;
    public VehicleSession(NativeObservationBus bus, IActivitySession activity, ISensorFusionSession fusion, TimeProvider? clock = null)
    { estimator = new(clock); this.bus = bus; this.activity = activity; this.fusion = fusion; bus.LocationReceived += Receive; }
    private void Receive(LocationObservation fix) => Update(fix, activity.IsForkliftDeclared, fusion.GetFusionSnapshot().Motion);
    public LocalPoint? GetGpsOrigin() { lock (gate) return estimator.GetGpsOrigin(); }
    public void SetGpsOrigin(double latitude, double longitude) { lock (gate) estimator.SetGpsOrigin(latitude, longitude); ContextChanged?.Invoke(new(GpsOrigin: new(longitude, latitude))); }
    public void Update(LocationObservation fix, bool declaredForklift, DeviceMotion? motion) { lock (gate) estimator.Update(fix, declaredForklift, motion); }
    public VehicleSnapshot GetSnapshot() { lock (gate) return activity.IsForkliftDeclared ? estimator.GetSnapshot() : estimator.GetSnapshot() with { Confidence = 0, SpeedMetersPerSecond = null, CourseRadians = null, Detail = "Unknown: truckläge är inte aktivt. XY är senaste historiska fix." }; }
    public void Dispose() => bus.LocationReceived -= Receive;
}
