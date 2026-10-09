using Sensor3.Contracts;
using Sensor3.Positioning;
namespace Sensor3.Tests;
[TestClass]
public sealed class VehicleTests
{
    private sealed class Clock : TimeProvider { public DateTimeOffset Now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
    private static LocationObservation Fix(Clock clock, double meters = 0, double? speed = 2) => new(0, meters / 6371000 * 180 / Math.PI, 1, null, speed, 90, clock.Now, false);
    [TestMethod]
    public void TestThat_vehicle_motion_uses_fresh_gps_and_separate_distance()
    {
        var clock = new Clock(); var model = new ForkliftMotionEstimator(clock); model.SetGpsOrigin(0, 0);
        model.Update(Fix(clock), true, null); clock.Now += TimeSpan.FromSeconds(1); model.Update(Fix(clock, 2), true, null);
        Assert.AreEqual(2d, model.GetSnapshot().DistanceMeters, 1e-6); Assert.AreEqual(Math.PI / 2, model.GetSnapshot().CourseRadians!.Value, 1e-8);
        clock.Now += TimeSpan.FromSeconds(6); Assert.IsNull(model.GetSnapshot().SpeedMetersPerSecond); Assert.AreEqual(0d, model.GetSnapshot().Confidence);
    }
    [TestMethod]
    [DataRow(false, false, DisplayName = "Walking context")]
    [DataRow(true, true, DisplayName = "Mock GPS")]
    public void TestThat_untrusted_reference_does_not_create_truck_motion(bool truck, bool mock)
    {
        var clock = new Clock(); var model = new ForkliftMotionEstimator(clock); model.SetGpsOrigin(0, 0); model.Update(Fix(clock) with { IsMock = mock }, truck, null);
        Assert.IsNull(model.GetSnapshot().Position); Assert.AreEqual(0d, model.GetSnapshot().DistanceMeters);
    }
    [TestMethod]
    public void TestThat_stationary_jitter_hand_vibration_and_gps_gap_do_not_add_distance()
    {
        var clock = new Clock(); var model = new ForkliftMotionEstimator(clock); model.SetGpsOrigin(0, 0);
        var hand = new DeviceMotion(0, new(30, 10, 5), new(30, 10, 5), new(30, 10, 5), new(1, 1, 1), .8);
        model.Update(Fix(clock, 0, 0), true, hand); clock.Now += TimeSpan.FromSeconds(1); model.Update(Fix(clock, .5, 0), true, hand);
        Assert.AreEqual(0d, model.GetSnapshot().DistanceMeters); Assert.IsNull(model.GetSnapshot().CourseRadians);
        clock.Now += TimeSpan.FromSeconds(10); model.Update(Fix(clock, 10), true, hand); Assert.AreEqual(0d, model.GetSnapshot().DistanceMeters); Assert.AreEqual(0d, model.GetSnapshot().Confidence);
    }
}
