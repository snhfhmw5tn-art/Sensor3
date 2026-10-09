using Sensor3.Contracts;
using Sensor3.Positioning;
namespace Sensor3.Tests;
[TestClass]
public sealed class RadioPositioningTests
{
    private static WifiObservation[] Scan(double offset = 0) => Enumerable.Range(1, 3).Select(i => new WifiObservation("synthetic", $"ap-{i}", -40 - i * 5 + offset, 2412, DateTimeOffset.UtcNow, null, false)).ToArray();
    [TestMethod]
    public void TestThat_fingerprint_reference_has_zero_synthetic_error()
    {
        var map = new RadioMap([new(new(4, 8), Scan(), DateTimeOffset.UtcNow, .7)], []); RadioFingerprintEstimator.Validate(map);
        var result = new RadioFingerprintEstimator().Wifi(map, Scan()); Assert.AreEqual(4d, result.Point!.X, 1e-8); Assert.AreEqual(8d, result.Point.Y, 1e-8);
        Assert.IsGreaterThanOrEqualTo(2d, result.UncertaintyMeters);
    }
    [TestMethod]
    public void TestThat_cached_missing_and_frequency_changed_wifi_are_unknown()
    {
        var estimator = new RadioFingerprintEstimator(); var map = new RadioMap([new(new(4, 8), Scan(), DateTimeOffset.UtcNow, .7)], []);
        Assert.IsNull(estimator.Wifi(map, Scan().Select(x => x with { IsCached = true }).ToArray()).Point);
        Assert.IsNull(estimator.Wifi(map, Scan().Take(2).ToArray()).Point);
        Assert.IsNull(estimator.Wifi(map, Scan().Select(x => x with { FrequencyMhz = 5200 }).ToArray()).Point);
        Assert.IsNull(estimator.Wifi(map, Scan(40)).Point);
    }
    [TestMethod]
    public void TestThat_ble_uses_known_anchor_radius_and_rejects_ambiguity()
    {
        var estimator = new RadioFingerprintEstimator(); var map = new RadioMap([], [new("a", new(1, 2), 5), new("b", new(8, 9), 5)]);
        Assert.AreEqual(new LocalPoint(1, 2), estimator.Bluetooth(map, [new("a", -50, "", DateTimeOffset.UtcNow)]).Point);
        Assert.IsNull(estimator.Bluetooth(map, [new("a", -50, "", DateTimeOffset.UtcNow), new("b", -51, "", DateTimeOffset.UtcNow)]).Point);
    }
    [TestMethod]
    public void TestThat_radio_correction_preserves_raw_path_and_walk_distance()
    {
        var pdr = new PedestrianPositionEstimator(); pdr.SetStart(0, 0); pdr.Add(new(1, .5, 1, false, 0, .8), new(0, .8, .1, "test", ""));
        var before = pdr.Snapshot(); pdr.Correct(new(new(4, 4), 2, .5, "test", ""));
        Assert.AreEqual(before.RawPosition, pdr.Snapshot().RawPosition); Assert.AreEqual(before.WalkingMeters, pdr.Snapshot().WalkingMeters);
        Assert.AreNotEqual(before.Position, pdr.Snapshot().Position);
    }
}
