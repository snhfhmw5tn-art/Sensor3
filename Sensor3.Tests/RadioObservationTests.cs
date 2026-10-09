using Sensor3.Contracts;
using Sensor3.Sensors;

namespace Sensor3.Tests;

[TestClass]
public sealed class RadioObservationTests
{
    [TestMethod]
    [DataRow(-91.0, 0.0, DisplayName = "Latitude outside globe")]
    [DataRow(0.0, 181.0, DisplayName = "Longitude outside globe")]
    [DataRow(double.NaN, 0.0, DisplayName = "NaN latitude")]
    public void TestThat_invalid_location_is_rejected(double latitude, double longitude) =>
        Assert.ThrowsExactly<InvalidDataException>(() => RadioObservationRules.ValidateLocation(new(latitude, longitude, null, null, null, null, DateTimeOffset.UtcNow, false)));
    [TestMethod]
    public void TestThat_missing_gnss_speed_heading_and_accuracy_remain_unknown()
    {
        var value = RadioObservationRules.ValidateLocation(new(57, 14, null, null, null, null, DateTimeOffset.UtcNow, true));
        Assert.IsNull(value.SpeedMetersPerSecond); Assert.IsNull(value.HeadingDegrees); Assert.IsNull(value.AccuracyMeters); Assert.IsTrue(value.IsMock);
    }
    [TestMethod]
    public void TestThat_wifi_is_deduplicated_by_bssid_without_interpreting_rssi_as_distance()
    {
        var now = DateTimeOffset.UtcNow;
        var values = RadioObservationRules.UniqueWifi([new("same-ssid", "AA", -80, 2412, now, 1234, true), new("same-ssid", "aa", -60, 2412, now, 1235, true), new("same-ssid", "BB", -70, 5180, now, null, false)]);
        Assert.HasCount(2, values); Assert.AreEqual(-60.0, values.Single(x => x.Bssid == "aa").RssiDbm);
        Assert.AreEqual(1235L, values.Single(x => x.Bssid == "aa").NativeTimestampMicroseconds);
    }
    [TestMethod]
    [DataRow(0, DisplayName = "Zero duration")]
    [DataRow(31, DisplayName = "Unbounded scan")]
    public void TestThat_scan_duration_is_bounded(int seconds) => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => RadioObservationRules.ValidateScanDuration(TimeSpan.FromSeconds(seconds)));
}
