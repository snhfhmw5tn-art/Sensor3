using Sensor3.CarryingMode;
using Sensor3.Contracts;

namespace Sensor3.Tests;

[TestClass]
public sealed class CarryingTests
{
    private static CarryingEstimate Run(ICarryingClassifier classifier, CarryingContext context, ActivityKind activity = ActivityKind.Walking, double start = 0,
        double tilt = 0, double gyro = .1, double vertical = .5, double horizontal = .2)
    {
        CarryingEstimate estimate = new(CarryingKind.Unknown, false, 0, new Dictionary<CarryingKind, double>(), 0, 0, "");
        for (var i = 0; i < 150; i++)
        {
            var time = start + i * .02; var wave = Math.Sin(2 * Math.PI * 2 * time); var q = QuaternionD.AxisAngle(new(1, 0, 0), tilt);
            var linear = new Vector3D(horizontal * wave, 0, vertical * wave);
            estimate = classifier.Update(new(time, q.Conjugate().Rotate(linear), linear, linear, new(0, 0, gyro), .8), new(time, q, 0, "synthetic", .8), new(activity, activity, .8, true, time, "test"), context);
        }
        return estimate;
    }
    [TestMethod]
    public void TestThat_pocket_evidence_and_hand_transitions_have_explicit_uncertainty()
    {
        var classifier = new BaselineCarryingClassifier(); Run(classifier, new(.1, 100));
        var puttingAway = Run(classifier, new(0, 1), start: 3);
        Assert.IsTrue(puttingAway.Kind is CarryingKind.PuttingAway or CarryingKind.Pocket);
        var pocket = Run(classifier, new(0, 1), start: 6); Assert.AreEqual(CarryingKind.Pocket, pocket.Kind);
        var pickup = Run(classifier, new(.1, 100), start: 9); Assert.IsTrue(pickup.Kind is CarryingKind.PickingUp or CarryingKind.HeldStable);
        Assert.AreEqual(1d, pocket.Probabilities.Values.Sum(), 1e-10); Assert.IsLessThan(1d, pocket.Confidence);
    }
    [TestMethod]
    public void TestThat_raw_pose_change_is_a_transition_without_position_or_step_side_effects()
    {
        var classifier = new BaselineCarryingClassifier(); Run(classifier, new(), tilt: 0);
        CarryingEstimate? transition = null;
        for (var i = 0; i < 150; i++)
        {
            var time = 3 + i * .02; var q = QuaternionD.AxisAngle(new(1, 0, 0), Math.PI / 2);
            var value = classifier.Update(new(time, Vector3D.Zero, Vector3D.Zero, Vector3D.Zero, new(.8, 0, 0), .8), new(time, q, null, "synthetic", .8), new(ActivityKind.Stationary, ActivityKind.Stationary, .8, true, time, "test"), new());
            if (value.Transition) transition = value;
        }
        Assert.IsNotNull(transition); Assert.IsGreaterThan(8d, transition.CrossWindowAccelerationChange);
    }
    [TestMethod]
    public void TestThat_declared_mounted_or_scanning_is_identified_as_operator_prior()
    {
        foreach (var kind in new[] { CarryingKind.Mounted, CarryingKind.Scanning })
        { var result = Run(new BaselineCarryingClassifier(), new(Declared: kind)); Assert.AreEqual(kind, result.Kind); StringAssert.Contains(result.Explanation, "operatör"); }
    }
    [TestMethod]
    public void TestThat_missing_gyro_or_orientation_remains_unknown()
    {
        var classifier = new BaselineCarryingClassifier(); var result = classifier.Update(new(0, Vector3D.Zero, Vector3D.Zero, Vector3D.Zero, null, .3), null,
            new(ActivityKind.Unknown, ActivityKind.Unknown, 0, false, 0, ""), new());
        Assert.AreEqual(CarryingKind.Unknown, result.Kind);
    }
    [TestMethod]
    public void TestThat_swinging_hand_changes_pose_without_a_counter_reset()
    {
        var classifier = new BaselineCarryingClassifier(); CarryingEstimate? result = null;
        for (var i = 0; i < 150; i++)
        {
            var time = i * .02; var wave = Math.Sin(2 * Math.PI * 2 * time); var q = QuaternionD.AxisAngle(new(1, 0, 0), .6 * wave);
            var linear = new Vector3D(.5 * wave, 0, 2 * wave);
            result = classifier.Update(new(time, q.Conjugate().Rotate(linear), linear, linear, new(.5, 0, 0), .8), new(time, q, 0, "synthetic", .8),
                new(ActivityKind.Walking, ActivityKind.Walking, .8, true, time, "test"), new());
        }
        Assert.IsNotNull(result); Assert.AreEqual(CarryingKind.HandSwinging, result.Kind);
        Assert.AreEqual(CarryingKind.Viewing, Run(new BaselineCarryingClassifier(), new(), tilt: .6).Kind);
        Assert.AreEqual(CarryingKind.HeldStable, Run(new BaselineCarryingClassifier(), new(), tilt: 0).Kind);
    }
    [TestMethod]
    public void TestThat_rotation_and_vertical_motion_are_separate_carry_hypotheses()
    {
        Assert.AreEqual(CarryingKind.HandRotating, Run(new BaselineCarryingClassifier(), new(), gyro: .9).Kind);
        Assert.AreEqual(CarryingKind.VerticalSwinging, Run(new BaselineCarryingClassifier(), new(), vertical: 2, horizontal: 0).Kind);
        Assert.AreEqual(CarryingKind.DeviceStationary, Run(new BaselineCarryingClassifier(), new(), ActivityKind.Stationary, gyro: 0, vertical: 0, horizontal: 0).Kind);
    }
}
