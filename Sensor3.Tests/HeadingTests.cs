using Sensor3.Contracts;
using Sensor3.SensorFusion;

namespace Sensor3.Tests;

[TestClass]
public sealed class HeadingTests
{
    private static (HumanHeading Heading, HeadingComparison Comparison) Scenario(Func<double, double> body, Func<double, double> phone,
        CarryingKind kind = CarryingKind.HeldStable, Func<double, bool>? transition = null)
    {
        var estimator = new AdaptiveHumanHeadingEstimator(); estimator.SetKnownStartHeading(0);
        double previousPhone = phone(0);
        for (var i = 0; i < 450; i++)
        {
            var time = i * .02; var angle = body(time); var phoneAngle = phone(time);
            var rate = i == 0 ? 0 : AdaptiveHumanHeadingEstimator.Difference(phoneAngle, previousPhone) / .02; previousPhone = phoneAngle;
            var wave = Math.Sin(2 * Math.PI * 2 * time); var world = new Vector3D(.5 * wave * Math.Sin(angle), .5 * wave * Math.Cos(angle), 2 * wave);
            var q = QuaternionD.AxisAngle(Vector3D.Up, -phoneAngle);
            estimator.Update(new(time, q.Conjugate().Rotate(world), world, world, new(0, 0, -rate), .8), new(time, q, phoneAngle, "synthetic", .8),
                new(ActivityKind.Walking, ActivityKind.Walking, .8, true, time, "synthetic test"),
                new(kind, transition?.Invoke(time) == true, .65, new Dictionary<CarryingKind, double>(), 0, 0, "synthetic"), 2);
        }
        return (estimator.GetHeading(), estimator.GetComparison());
    }
    private static double Turn(double time, double angle) => angle * Math.Clamp((time - 3) / 1, 0, 1);
    [TestMethod]
    [DataRow(5d, DisplayName = "Phone 5 degrees")]
    [DataRow(10d, DisplayName = "Phone 10 degrees")]
    [DataRow(15d, DisplayName = "Phone 15 degrees")]
    [DataRow(20d, DisplayName = "Phone 20 degrees")]
    [DataRow(30d, DisplayName = "Phone 30 degrees")]
    [DataRow(45d, DisplayName = "Phone 45 degrees")]
    [DataRow(60d, DisplayName = "Phone 60 degrees")]
    [DataRow(90d, DisplayName = "Phone 90 degrees")]
    [DataRow(180d, DisplayName = "Phone 180 degrees")]
    public void TestThat_phone_rotation_does_not_change_straight_person_heading(double degrees)
    {
        var angle = degrees * Math.PI / 180; var result = Scenario(_ => 0, time => Turn(time, angle));
        Assert.AreEqual(0d, result.Heading.Radians!.Value, .01);
        Assert.AreEqual(0d, AdaptiveHumanHeadingEstimator.Difference(result.Comparison.GyroOnlyRadians!.Value, angle), .01);
    }
    [TestMethod]
    [DataRow(5d, DisplayName = "Person 5 degrees")]
    [DataRow(10d, DisplayName = "Person 10 degrees")]
    [DataRow(15d, DisplayName = "Person 15 degrees")]
    [DataRow(20d, DisplayName = "Person 20 degrees")]
    [DataRow(30d, DisplayName = "Person 30 degrees")]
    [DataRow(45d, DisplayName = "Person 45 degrees")]
    [DataRow(60d, DisplayName = "Person 60 degrees")]
    [DataRow(90d, DisplayName = "Person 90 degrees")]
    [DataRow(180d, DisplayName = "Person 180 degrees")]
    public void TestThat_sustained_person_turn_is_detected_from_world_gait(double degrees)
    {
        var angle = degrees * Math.PI / 180; var result = Scenario(time => Turn(time, angle), time => Turn(time, angle));
        Assert.AreEqual(0d, AdaptiveHumanHeadingEstimator.Difference(result.Heading.Radians!.Value, angle), .03);
        Assert.IsGreaterThan(.5, result.Heading.Confidence);
    }
    [TestMethod]
    [DataRow(CarryingKind.Pocket, DisplayName = "Pocket")]
    [DataRow(CarryingKind.HandSwinging, DisplayName = "Swinging hand")]
    [DataRow(CarryingKind.HandRotating, DisplayName = "Phone opposite body turn")]
    public void TestThat_carry_and_opposite_phone_turn_do_not_control_body_heading(CarryingKind kind)
    {
        var result = Scenario(time => Turn(time, Math.PI / 2), time => -Turn(time, Math.PI / 2), kind);
        Assert.AreEqual(0d, AdaptiveHumanHeadingEstimator.Difference(result.Heading.Radians!.Value, Math.PI / 2), .03);
    }
    [TestMethod]
    public void TestThat_phone_pickup_during_turn_is_uncertain_then_recovers_from_gait()
    {
        var result = Scenario(time => Turn(time, Math.PI / 2), time => Turn(time, Math.PI), CarryingKind.Pocket, time => time is >= 3 and <= 5);
        Assert.AreEqual(0d, AdaptiveHumanHeadingEstimator.Difference(result.Heading.Radians!.Value, Math.PI / 2), .03);
    }
    [TestMethod]
    public void TestThat_s_curve_returns_heading_to_straight_without_following_phone_yaw()
    {
        double Curve(double time) => time < 3 || time > 7 ? 0 : .7 * Math.Sin((time - 3) * Math.PI / 2);
        var result = Scenario(Curve, time => -Curve(time) + Turn(time, Math.PI / 4), CarryingKind.HandSwinging);
        Assert.AreEqual(0d, result.Heading.Radians!.Value, .03);
    }
    [TestMethod]
    public void TestThat_phase_signed_axis_can_resolve_pca_reversal_in_controlled_gait()
    {
        var result = Scenario(time => time < 3 ? 0 : Math.PI, _ => 0, CarryingKind.Pocket);
        Assert.AreEqual(0d, result.Comparison.PcaRadians!.Value, .03);
        Assert.AreEqual(0d, AdaptiveHumanHeadingEstimator.Difference(result.Heading.Radians!.Value, Math.PI), .03);
    }
    [TestMethod]
    public void TestThat_missing_start_weak_phase_and_sensor_gap_do_not_invent_heading()
    {
        var estimator = new AdaptiveHumanHeadingEstimator();
        var motion = new DeviceMotion(0, Vector3D.Zero, Vector3D.Zero, Vector3D.Zero, Vector3D.Zero, .8);
        var activity = new ActivityEstimate(ActivityKind.Walking, ActivityKind.Walking, .8, true, 0, "test");
        var carrying = new CarryingEstimate(CarryingKind.HeldStable, false, .65, new Dictionary<CarryingKind, double>(), 0, 0, "test");
        var orientation = new DeviceOrientation(0, QuaternionD.Identity, 0, "synthetic", .8);
        Assert.IsNull(estimator.Update(motion, orientation, activity, carrying).Radians);
        estimator.SetKnownStartHeading(0);
        for (var i = 0; i < 150; i++) estimator.Update(motion with { Seconds = i * .02, FilteredWorldAcceleration = new(Math.Sin(i), 0, 0) }, orientation, activity, carrying, 2);
        Assert.AreEqual(0d, estimator.GetHeading().Confidence); Assert.AreEqual(Math.PI, estimator.GetHeading().UncertaintyRadians);
        estimator.Update(motion with { Seconds = 10 }, orientation, activity, carrying); Assert.IsNull(estimator.GetHeading().Radians);
    }
}
