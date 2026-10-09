using Sensor3.Contracts;
using Sensor3.Positioning;
namespace Sensor3.Tests;
[TestClass]
public sealed class PositioningTests
{
    [TestMethod]
    [DataRow(10d, DisplayName = "Synthetic 10 m")]
    [DataRow(20d, DisplayName = "Synthetic 20 m")]
    [DataRow(40d, DisplayName = "Synthetic 40 m")]
    [DataRow(100d, DisplayName = "Synthetic 100 m")]
    public void TestThat_calibrated_steps_follow_known_distance(double distance)
    {
        var pdr = new PedestrianPositionEstimator(new(.5, .65)); pdr.SetStart(0, 0);
        for (var i = 1; i <= distance * 2; i++) pdr.Add(new(i * .5, .5, 1, false, 0, .8), new(0, .8, .1, "synthetic", ""));
        Assert.AreEqual(distance, pdr.Snapshot().Position!.Y, 1e-8); Assert.AreEqual(distance, pdr.Snapshot().WalkingMeters, 1e-8);
    }
    [TestMethod]
    public void TestThat_missing_heading_grows_uncertainty_without_inventing_position()
    {
        var pdr = new PedestrianPositionEstimator(); pdr.SetStart(3, 4);
        pdr.Add(new(1, .5, 1, false, 0, .8), new(null, 0, Math.PI, "Unknown", ""));
        Assert.AreEqual(new LocalPoint(3, 4), pdr.Snapshot().Position); Assert.IsGreaterThan(1, pdr.Snapshot().UncertaintyMeters);
    }
    [TestMethod]
    public void TestThat_turns_running_duplicates_and_interruption_are_separate()
    {
        var pdr = new PedestrianPositionEstimator(new(.5, 1)); pdr.SetStart(0, 0);
        pdr.Add(new(1, .5, 1, false, 0, .8), new(0, .8, .1, "test", ""));
        pdr.Add(new(2, .5, 1, true, 0, .8), new(Math.PI / 2, .8, .1, "test", ""));
        Assert.AreEqual(0d, pdr.Add(new(2, .5, 1, true, 0, .8), new(0, .8, .1, "test", "")));
        Assert.AreEqual(1d, pdr.Snapshot().RunningMeters); Assert.AreEqual(1d, pdr.Snapshot().Position!.X, 1e-8);
        pdr.Stop(); Assert.IsNull(pdr.Snapshot().SpeedMetersPerSecond);
    }
}
