using Sensor3.Contracts;
using Sensor3.ActivityRecognition;

namespace Sensor3.Tests;

[TestClass]
public sealed class ActivityTests
{
    private static GaitFeatures Walking() => new(1.4, 2, .08, .1, 4, 2, .9, 50, true);
    private static GaitFeatures Stationary() => new(.03, .0001, .0001, .01, .1, 0, 0, 50, true);
    private static ActivityEstimate Run(IActivityClassifier classifier, GaitFeatures? features, ActivityContext? context = null, double start = 0)
    {
        ActivityEstimate result = new(ActivityKind.Unknown, ActivityKind.Unknown, 0, false, 0, "");
        for (var i = 0; i < 100; i++) result = classifier.Update(features, start + i * .02, context ?? new());
        return result;
    }
    [TestMethod]
    public void TestThat_activity_baseline_distinguishes_stationary_walking_and_running()
    {
        Assert.AreEqual(ActivityKind.Stationary, Run(new BaselineActivityClassifier(), Stationary()).Kind);
        Assert.AreEqual(ActivityKind.Walking, Run(new BaselineActivityClassifier(), Walking()).Kind);
        Assert.AreEqual(ActivityKind.Running, Run(new BaselineActivityClassifier(), Walking() with { Rms = 3, CadenceHz = 3 }).Kind);
    }
    [TestMethod]
    public void TestThat_unknown_is_used_for_missing_low_rate_or_irregular_motion()
    {
        Assert.AreEqual(ActivityKind.Unknown, Run(new BaselineActivityClassifier(), null).Kind);
        Assert.AreEqual(ActivityKind.Unknown, Run(new BaselineActivityClassifier(), Walking() with { SampleRateHz = 5 }).Kind);
        Assert.AreEqual(ActivityKind.Unknown, Run(new BaselineActivityClassifier(), Walking() with { Periodicity = .1 }).Kind);
        Assert.AreEqual(ActivityKind.Unknown, Run(new BaselineActivityClassifier(), Walking() with { Rms = double.NaN }).Kind);
    }
    [TestMethod]
    public void TestThat_transitions_require_sustained_evidence_and_gaps_remove_old_state()
    {
        var classifier = new BaselineActivityClassifier(); var initial = classifier.Update(Walking(), 0, new()); Assert.IsFalse(initial.Stable);
        for (var i = 1; i < 20; i++) Assert.AreEqual(ActivityKind.Unknown, classifier.Update(Walking(), i * .02, new()).Kind);
        var result = Run(classifier, Walking(), start: .4); Assert.AreEqual(ActivityKind.Walking, result.Kind);
        var transition = classifier.Update(Stationary(), 2.4, new()); Assert.AreEqual(ActivityKind.Stationary, transition.Candidate); Assert.IsFalse(transition.Stable);
        Assert.AreEqual(ActivityKind.Unknown, classifier.Update(Walking(), 10, new()).Kind);
    }
    [TestMethod]
    public void TestThat_forklift_requires_declared_prior_and_gps_does_not_invent_truck_type()
    {
        var gps = Run(new BaselineActivityClassifier(), Walking() with { Periodicity = .1 }, new(GpsSpeedMetersPerSecond: 4, TrustedGps: true));
        Assert.AreEqual(ActivityKind.Unknown, gps.Kind);
        var declared = Run(new BaselineActivityClassifier(), Stationary(), new(DeclaredForklift: true));
        Assert.AreEqual(ActivityKind.Forklift, declared.Kind); StringAssert.Contains(declared.Explanation, "operatören"); Assert.IsLessThanOrEqualTo(.65, declared.Confidence);
    }
}
