using Sensor3.Contracts;
using Sensor3.SensorFusion;

namespace Sensor3.Tests;

[TestClass]
public sealed class FusionTests
{
    private static SensorReading Reading(SensorKind kind, double seconds, Vector3D vector, SensorTimestampSource source = SensorTimestampSource.AndroidElapsedRealtime, double? w = null)
    {
        var values = new List<SensorValue> { new("x", vector.X, "SI"), new("y", vector.Y, "SI"), new("z", vector.Z, "SI") };
        if (w is { } scalar) values.Add(new("w", scalar, "1"));
        return new(kind.ToString(), kind, values, source == SensorTimestampSource.AndroidElapsedRealtime ? (long)(seconds * 1e9) : null,
            source == SensorTimestampSource.WindowsUtc ? DateTimeOffset.UnixEpoch.AddSeconds(seconds) : null, DateTimeOffset.UtcNow, source, SensorQuality.High, "test device");
    }
    [TestMethod]
    [DataRow(1d, 0d, 0d, 0d, 0d, 1d, DisplayName = "X rotation takes Y to Z")]
    [DataRow(0d, 1d, 0d, 0d, 1d, 0d, DisplayName = "Y rotation preserves Y")]
    [DataRow(0d, 0d, 1d, -1d, 0d, 0d, DisplayName = "Z rotation takes Y to minus X")]
    public void TestThat_quaternion_and_matrix_agree_on_axis_rotation(double ax, double ay, double az, double ex, double ey, double ez)
    {
        var q = QuaternionD.AxisAngle(new(ax, ay, az), Math.PI / 2); var expected = new Vector3D(ex, ey, ez);
        Assert.AreEqual(0d, (q.Rotate(new(0, 1, 0)) - expected).Length, 1e-10);
        var matrix = q.Matrix(); Assert.AreEqual(0d, (new Vector3D(matrix[0, 1], matrix[1, 1], matrix[2, 1]) - expected).Length, 1e-10);
        Assert.AreEqual(0d, (q.Conjugate().Rotate(q.Rotate(new(1, 2, 3))) - new Vector3D(1, 2, 3)).Length, 1e-10);
    }
    [TestMethod]
    public void TestThat_antiparallel_gravity_alignment_and_invalid_quaternion_are_handled()
    {
        Assert.AreEqual(0d, (QuaternionD.Align(new(0, 0, -1), Vector3D.Up).Rotate(new(0, 0, -1)) - Vector3D.Up).Length, 1e-10);
        Assert.ThrowsExactly<ArgumentException>(() => new QuaternionD(0, 0, 0, 0).Normalized());
    }
    [TestMethod]
    public void TestThat_alignment_interpolates_and_rejects_old_samples()
    {
        var buffer = new TimeAlignedVectorBuffer(); buffer.Add(1, new(0, 0, 0)); buffer.Add(1.1, new(2, 0, 0));
        Assert.AreEqual(1d, buffer.At(1.05)!.Value.X, 1e-10); Assert.IsNull(buffer.At(2)); Assert.IsFalse(buffer.Add(1.05, Vector3D.Up));
    }
    [TestMethod]
    [DataRow(SensorTimestampSource.AndroidElapsedRealtime, 1d, DisplayName = "Android upward specific force")]
    [DataRow(SensorTimestampSource.WindowsUtc, -1d, DisplayName = "Windows negative G-force")]
    public void TestThat_platform_gravity_signs_produce_zero_stationary_world_acceleration(SensorTimestampSource source, double sign)
    {
        var fusion = new MotionFusion(); fusion.Process(Reading(SensorKind.Gravity, 1, new(0, 0, sign * 9.80665), source));
        var motion = fusion.Process(Reading(SensorKind.Accelerometer, 1, new(0, 0, sign * 9.80665), source));
        Assert.IsNotNull(motion); Assert.AreEqual(0d, motion.WorldLinearAcceleration.Length, 1e-10);
    }
    [TestMethod]
    public void TestThat_tilted_phone_gravity_is_removed_without_creating_translation()
    {
        var fusion = new MotionFusion(); var q = QuaternionD.AxisAngle(new(1, 0, 0), .8);
        fusion.Process(Reading(SensorKind.RotationVector, 1, new(q.X, q.Y, q.Z), w: q.W));
        var g = q.Conjugate().Rotate(Vector3D.Up * 9.80665);
        fusion.Process(Reading(SensorKind.Gravity, 1, g));
        var motion = fusion.Process(Reading(SensorKind.Accelerometer, 1, g));
        Assert.IsNotNull(motion); Assert.AreEqual(0d, motion.WorldLinearAcceleration.Length, 1e-10);
        Assert.AreEqual(0d, motion.FilteredWorldAcceleration.Length, 1e-10);
    }
    [TestMethod]
    public void TestThat_bias_calibration_requires_explicit_request_and_quiet_measurements()
    {
        var fusion = new MotionFusion();
        for (var i = 0; i < 100; i++)
        {
            var time = i * .02; fusion.Process(Reading(SensorKind.Accelerometer, time, new(0, 0, 9.80665)));
            fusion.Process(Reading(SensorKind.Gyroscope, time, new(.01, 0, 0)));
        }
        Assert.IsFalse(fusion.GetFusionSnapshot().BiasCalibrated); fusion.BeginGyroCalibration();
        for (var i = 100; i < 150; i++)
        {
            var time = i * .02; fusion.Process(Reading(SensorKind.Accelerometer, time, new(0, 0, 9.80665)));
            fusion.Process(Reading(SensorKind.Gyroscope, time, new(.01, 0, 0)));
        }
        var snapshot = fusion.GetFusionSnapshot(); Assert.IsTrue(snapshot.BiasCalibrated); Assert.AreEqual(.01, snapshot.GyroBias.X, 1e-10); Assert.IsNotNull(snapshot.GyroNoiseRms);
    }
    [TestMethod]
    public void TestThat_equal_accel_and_linear_timestamps_do_not_discard_linear_sensor()
    {
        var fusion = new MotionFusion(); fusion.Process(Reading(SensorKind.Accelerometer, 1, new(0, 0, 9.80665)), true);
        var motion = fusion.Process(Reading(SensorKind.LinearAcceleration, 1, new(.2, 0, 0)), true);
        Assert.IsNotNull(motion); Assert.AreEqual(.2, motion.WorldLinearAcceleration.X, 1e-10);
    }
}
