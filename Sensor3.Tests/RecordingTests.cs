using Sensor3.Contracts;
using Sensor3.Simulation;
namespace Sensor3.Tests;
[TestClass]
public sealed class RecordingTests
{
    [TestMethod]
    public void TestThat_all_synthetic_scenarios_use_shared_pipeline_and_remain_labeled()
    {
        foreach (var scenario in Enum.GetValues<SyntheticScenario>())
        {
            var recording = SyntheticScenarios.Create(scenario, 12); using var replay = new ReplaySession(recording); replay.AdvanceTo(30);
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { Scenario = scenario.ToString(), Result = replay.Compare() }));
            Assert.IsTrue(recording.Synthetic); Assert.AreEqual(recording.Frames.Count, replay.Index); Assert.Contains("SYNTHETIC", replay.Compare().Detail);
            Assert.IsTrue(double.IsFinite(replay.Compare().ElapsedMilliseconds)); Assert.IsGreaterThan(0L, replay.Compare().JsonBytes);
            if (scenario is SyntheticScenario.ForkliftDriving or SyntheticScenario.ForkliftVibration or SyntheticScenario.GpsGap or SyntheticScenario.Stationary) Assert.AreEqual(0L, replay.Analysis.Steps.GetSnapshot().Total, scenario.ToString());
        }
    }
    [TestMethod]
    public void TestThat_json_csv_preserve_native_timestamps_quotes_and_ground_truth()
    {
        var source = SyntheticScenarios.Create(SyntheticScenario.StraightWalking, 5) with { Name = "name,\"quoted\"\nline" };
        var json = RecordingCodec.ToJson(source); Assert.AreEqual(json, RecordingCodec.ToJson(RecordingCodec.FromJson(json)));
        Assert.AreEqual(json, RecordingCodec.ToJson(RecordingCodec.FromCsv(RecordingCodec.ToCsv(source))));
    }
    [TestMethod]
    public void TestThat_seeking_recreates_model_without_duplicate_steps()
    {
        var source = SyntheticScenarios.Create(SyntheticScenario.StraightWalking, 12); using var replay = new ReplaySession(source); replay.AdvanceTo(12); var total = replay.Analysis.Steps.GetSnapshot().Total;
        Assert.IsGreaterThan(10L, total); replay.Seek(5); replay.AdvanceTo(12); Assert.AreEqual(total, replay.Analysis.Steps.GetSnapshot().Total);
    }
    [TestMethod]
    public void TestThat_missing_truth_is_unknown_and_malformed_import_is_rejected()
    {
        var source = SyntheticScenarios.Create(SyntheticScenario.Stationary, 5); var noTruth = source with { Frames = source.Frames.Select(x => x with { Truth = null }).ToArray() };
        using var replay = new ReplaySession(noTruth); replay.AdvanceTo(5); Assert.IsNull(replay.Compare().StepError); Assert.IsNull(replay.Compare().ClassificationPrecision);
        Assert.ThrowsExactly<System.IO.InvalidDataException>(() => RecordingCodec.Validate(source with { Frames = [new(-1, source.Frames[0].Event)] }));
        Assert.ThrowsExactly<System.IO.InvalidDataException>(() => RecordingCodec.FromCsv("bad"));
    }
}
