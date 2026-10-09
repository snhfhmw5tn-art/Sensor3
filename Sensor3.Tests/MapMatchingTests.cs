using Sensor3.Contracts;
using Sensor3.MapMatching;
namespace Sensor3.Tests;
[TestClass]
public sealed class MapMatchingTests
{
    [TestMethod]
    public void TestThat_map_rejects_crossing_walls_racks_and_blocked_rectangles()
    {
        var map = new IndoorMap([new(MapFeatureKind.Wall, new(0,-10), new(0,10), "wall"), new(MapFeatureKind.Rack, new(5,5), new(8,8), "rack")]);
        Assert.IsFalse(IndoorParticleFilter.Allowed(new(-1,0), new(1,0), map));
        Assert.IsFalse(IndoorParticleFilter.Allowed(new(4,6), new(9,6), map));
        Assert.IsTrue(IndoorParticleFilter.Allowed(new(-2,0), new(-1,1), map));
    }
    [TestMethod]
    public void TestThat_particle_match_is_bounded_and_requires_real_geometry()
    {
        var filter = new IndoorParticleFilter(seed: 7);
        Assert.IsNull(filter.Update(new(0,0), 1, new([])).Point);
        var map = new IndoorMap([new(MapFeatureKind.Aisle, new(-10,0), new(10,0), "synthetic aisle")]);
        var result = filter.Update(new(0,1), 1, map); Assert.IsNotNull(result.Point); Assert.IsLessThan(2d, Math.Abs(result.Point.Y));
        Assert.IsTrue(result.SurvivingParticles is > 0 and <= 256);
    }
    [TestMethod]
    public void TestThat_geo_calibration_rotates_local_north_and_preserves_origin()
    {
        var calibration = new GeoCalibration(0,0,90); var geo = calibration.Convert(new(0,10));
        Assert.AreEqual(0d, geo.Latitude, 1e-8); Assert.IsGreaterThan(0d, geo.Longitude);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new GeoCalibration(90,0,0).Convert(new(0,0)));
    }
}
