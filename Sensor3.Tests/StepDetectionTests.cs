using Sensor3.Contracts;
using Sensor3.StepDetection;

namespace Sensor3.Tests;

[TestClass]
public sealed class StepDetectionTests
{
    private static GaitSample Sample(double time, double frequency = 2, double horizontal = .4, double gyro = 0, bool reliable = true) =>
        new(time, 2 * Math.Sin(2 * Math.PI * frequency * time), horizontal * Math.Sin(2 * Math.PI * frequency * time), 0, gyro, reliable);
    [TestMethod]
    public void TestThat_regular_gait_confirms_each_step_once_after_startup()
    {
        var detector = new GaitStepDetector(); var steps = new List<StepEvent>();
        for (var i = 0; i < 500; i++) steps.AddRange(detector.Update(Sample(i * .02)));
        Assert.IsTrue(steps.Count is >= 17 and <= 20, $"Detected {steps.Count}");
        Assert.AreEqual(steps.Count, steps.Select(x => x.Seconds).Distinct().Count());
        Assert.IsTrue(steps.All(x => x.Confidence is >= .6 and <= .85 && x.LengthMeters == .7));
    }
    [TestMethod]
    [DataRow(0d, 0d, true, DisplayName = "Vertical phone lifts")]
    [DataRow(.4, 2d, true, DisplayName = "Rapid hand rotation")]
    [DataRow(.4, 0d, false, DisplayName = "Missing orientation")]
    public void TestThat_insufficient_walking_evidence_is_rejected(double horizontal, double gyro, bool reliable)
    {
        var detector = new GaitStepDetector(); var count = 0;
        for (var i = 0; i < 500; i++) count += detector.Update(Sample(i * .02, horizontal: horizontal, gyro: gyro, reliable: reliable)).Count;
        Assert.AreEqual(0, count);
    }
    [TestMethod]
    public void TestThat_stationary_rotation_and_single_spikes_do_not_create_steps()
    {
        var detector = new GaitStepDetector();
        for (var i = 0; i < 500; i++) Assert.HasCount(0, detector.Update(new(i * .02, i == 200 ? 3 : 0, .4, 0, .8, true)));
    }
    [TestMethod]
    public void TestThat_duplicate_time_and_sensor_gap_do_not_repeat_confirmed_steps()
    {
        var detector = new GaitStepDetector(); var steps = new List<StepEvent>();
        for (var i = 0; i < 200; i++) { var sample = Sample(i * .02); steps.AddRange(detector.Update(sample)); Assert.HasCount(0, detector.Update(sample)); }
        var count = steps.Count;
        for (var i = 500; i < 700; i++) steps.AddRange(detector.Update(Sample(i * .02)));
        Assert.IsGreaterThan(count, steps.Count);
        Assert.AreEqual(steps.Count, steps.Select(x => x.Seconds).Distinct().Count());
    }
    [TestMethod]
    public void TestThat_regular_high_energy_running_has_separate_running_steps()
    {
        var detector = new GaitStepDetector(); var steps = new List<StepEvent>();
        for (var i = 0; i < 500; i++)
        {
            var time = i * .02; var wave = Math.Sin(2 * Math.PI * 3 * time);
            steps.AddRange(detector.Update(new(time, 3.5 * wave, .8 * wave, 0, 0, true)));
        }
        Assert.IsGreaterThanOrEqualTo(25, steps.Count); Assert.IsTrue(steps.All(x => x.Running && x.LengthMeters == 1));
    }
    [TestMethod]
    public void TestThat_parameters_reject_nonfinite_or_negative_lengths() => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new GaitStepDetector(new() { WalkingLength = double.NaN }));
}
