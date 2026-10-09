using Sensor3.Contracts;

namespace Sensor3.SensorFusion;

public sealed record FusionOptions
{
    public double Gravity { get; init; } = 9.80665;
    public double MaximumSensorAgeSeconds { get; init; } = .2;
    public double FilterTimeConstantSeconds { get; init; } = .04;
    public double GravityCutoffHz { get; init; } = .3;
    public double QuietAcceleration { get; init; } = .16;
    public double QuietGyroRadians { get; init; } = .05;
    public void Validate()
    {
        if (new[] { Gravity, MaximumSensorAgeSeconds, FilterTimeConstantSeconds, GravityCutoffHz, QuietAcceleration, QuietGyroRadians }.Any(x => !double.IsFinite(x) || x <= 0))
            throw new ArgumentOutOfRangeException(nameof(FusionOptions));
    }
}
public sealed class MotionFusion : ISensorFusionSession
{
    private readonly FusionOptions options;
    private readonly TimeAlignedVectorBuffer gravity = new(), gyro = new();
    private readonly Dictionary<SensorKind, double> lastTimes = [];
    private readonly List<(double Time, Vector3D Gyro)> quiet = [];
    private readonly List<Vector3D> quietAcceleration = [];
    private QuaternionD orientation = QuaternionD.Identity;
    private double orientationAt = double.NegativeInfinity, lastGyro = double.NegativeInfinity, lastMotion = double.NegativeInfinity, gravityAt = double.NegativeInfinity;
    private Vector3D gravityEstimate, bias, filtered;
    private bool calibrated, hasOrientation, calibrationRequested;
    private double? gyroNoise, accelerationNoise;
    private string frame = "local level frame; yaw unanchored";
    private FusionSnapshot snapshot = new(null, null, Vector3D.Zero, null, null, false, "Väntar på IMU.");
    public MotionFusion(FusionOptions? options = null) { this.options = options ?? new(); this.options.Validate(); }
    public FusionSnapshot GetFusionSnapshot() => snapshot;
    public void BeginGyroCalibration() { calibrationRequested = true; calibrated = false; quiet.Clear(); gyroNoise = null; }
    public void ResetEvidence()
    {
        gravity.Clear(); gyro.Clear(); lastTimes.Clear(); quiet.Clear(); quietAcceleration.Clear();
        orientationAt = lastGyro = lastMotion = gravityAt = double.NegativeInfinity; hasOrientation = false; orientation = QuaternionD.Identity;
        snapshot = snapshot with { Orientation = null, Motion = null, Detail = "Sensorunderlaget återinitialiseras; kalibrerad bias behålls." };
    }
    public static Vector3D ReadVector(SensorReading reading) => new(Channel(reading, "x"), Channel(reading, "y"), Channel(reading, "z"));
    private static double Channel(SensorReading reading, string name) => reading.Values.FirstOrDefault(x => x.Name == name)?.Value ?? double.NaN;
    public DeviceMotion? Process(SensorReading reading, bool preferLinearAcceleration = false)
    {
        var seconds = reading.MonotonicTimestampNanoseconds / 1e9 ?? reading.TimestampUtc?.ToUnixTimeMilliseconds() / 1000d;
        if (seconds is not { } time || !double.IsFinite(time)) return null;
        if (lastTimes.TryGetValue(reading.Kind, out var previousTime) && time <= previousTime) return null;
        var vector = ReadVector(reading);
        if (!vector.IsFinite || reading.Quality == SensorQuality.Unreliable) return null;
        lastTimes[reading.Kind] = time;
        // Windows G-force has the opposite sign to Android specific force.
        if (reading.TimestampSource == SensorTimestampSource.WindowsUtc && reading.Kind is SensorKind.Accelerometer or SensorKind.Gravity or SensorKind.LinearAcceleration) vector *= -1;
        if (reading.Kind == SensorKind.RotationVector)
        {
            if (time <= orientationAt) return null;
            var w = reading.Values.FirstOrDefault(x => x.Name == "w")?.Value ?? Math.Sqrt(Math.Max(0, 1 - Vector3D.Dot(vector, vector)));
            try { orientation = new QuaternionD(vector.X, vector.Y, vector.Z, w).Normalized(); }
            catch (ArgumentException) { return null; }
            orientationAt = time; hasOrientation = true;
            frame = reading.TimestampSource == SensorTimestampSource.AndroidElapsedRealtime ? "Android native world; north or game-yaw reference depends on sensor" : "Windows native world; geographic alignment unverified";
            UpdateSnapshot(time); return null;
        }
        if (reading.Kind == SensorKind.Gravity) { gravity.Add(time, vector); return null; }
        if (reading.Kind == SensorKind.Gyroscope)
        {
            if (!gyro.Add(time, vector)) return null;
            var linear = snapshot.Motion;
            var stationary = linear is not null && Math.Abs(time - linear.Seconds) <= options.MaximumSensorAgeSeconds && linear.DeviceLinearAcceleration.Length < options.QuietAcceleration && vector.Length < options.QuietGyroRadians;
            if (!calibrated && calibrationRequested)
            {
                if (stationary)
                {
                    quiet.Add((time, vector)); while (quiet.Count > 200) quiet.RemoveAt(0);
                    if (quiet.Count >= 20 && time - quiet[0].Time >= .6)
                    {
                        bias = quiet.Aggregate(Vector3D.Zero, (sum, x) => sum + x.Gyro) / quiet.Count;
                        gyroNoise = Math.Sqrt(quiet.Average(x => Math.Pow((x.Gyro - bias).Length, 2))); calibrated = true;
                    }
                }
                else quiet.Clear();
            }
            var elapsed = time - lastGyro;
            if (hasOrientation && time - orientationAt > options.MaximumSensorAgeSeconds && elapsed is > 0 and <= .2)
            {
                var corrected = vector - bias;
                if (corrected.Length > 1e-12) orientation = (orientation * QuaternionD.AxisAngle(corrected, corrected.Length * elapsed)).Normalized();
                frame = "local gyro/gravity frame; yaw drifts";
            }
            lastGyro = time; UpdateSnapshot(time); return null;
        }
        if (reading.Kind is not (SensorKind.Accelerometer or SensorKind.LinearAcceleration)) return null;
        var delta = double.IsFinite(lastMotion) ? time - lastMotion : .02;
        if (delta <= 0) return null;
        if (delta > .5) { quiet.Clear(); quietAcceleration.Clear(); hasOrientation = false; }
        var matchingGravity = gravity.At(time, options.MaximumSensorAgeSeconds);
        if (matchingGravity is null && reading.Kind == SensorKind.Accelerometer)
        {
            var gravityDelta = double.IsFinite(gravityAt) ? time - gravityAt : .02;
            gravityEstimate = double.IsFinite(gravityAt) ? gravityEstimate + (vector - gravityEstimate) * (1 - Math.Exp(-2 * Math.PI * options.GravityCutoffHz * Math.Min(gravityDelta, .5))) : vector;
            gravityAt = time; matchingGravity = gravityEstimate;
        }
        if (matchingGravity is null && Math.Abs(time - gravityAt) <= options.MaximumSensorAgeSeconds) matchingGravity = gravityEstimate;
        if (matchingGravity is null && hasOrientation && Math.Abs(time - orientationAt) <= options.MaximumSensorAgeSeconds)
            matchingGravity = orientation.Conjugate().Rotate(Vector3D.Up * options.Gravity);
        if (matchingGravity is not { } g || g.Length is < 7 or > 12)
        { snapshot = snapshot with { Motion = null, Detail = "Matchande gravity/orientation saknas; ingen världsrörelse beräknas." }; return null; }
        if (!hasOrientation)
        { orientation = QuaternionD.Align(g, Vector3D.Up); hasOrientation = true; frame = "local level frame; yaw unanchored"; }
        else if (time - orientationAt > options.MaximumSensorAgeSeconds)
        {
            var correction = QuaternionD.Align(orientation.Rotate(g.Normalized()), Vector3D.Up);
            var axis = new Vector3D(correction.X, correction.Y, correction.Z);
            if (axis.Length > 1e-12)
            {
                var angle = 2 * Math.Atan2(axis.Length, correction.W);
                var gain = gyro.At(time, options.MaximumSensorAgeSeconds) is null ? 1 : 1 - Math.Exp(-delta / 2);
                orientation = (QuaternionD.AxisAngle(axis, angle * gain) * orientation).Normalized();
            }
        }
        if (reading.Kind == SensorKind.Accelerometer && preferLinearAcceleration) return null;
        var linearVector = reading.Kind == SensorKind.LinearAcceleration ? vector : vector - g;
        var world = orientation.Rotate(linearVector);
        filtered = double.IsFinite(lastMotion) && delta <= .5 ? filtered + (world - filtered) * (1 - Math.Exp(-delta / options.FilterTimeConstantSeconds)) : world;
        var matchedGyro = gyro.At(time, options.MaximumSensorAgeSeconds);
        var angular = matchedGyro is { } angularVector ? orientation.Rotate(angularVector - bias) : (Vector3D?)null;
        var confidence = Math.Abs(time - orientationAt) <= options.MaximumSensorAgeSeconds ? .8 : matchedGyro is not null ? .5 : .3;
        if (linearVector.Length < options.QuietAcceleration)
        {
            quietAcceleration.Add(linearVector); if (quietAcceleration.Count > 100) quietAcceleration.RemoveAt(0);
            if (quietAcceleration.Count >= 20)
            {
                var average = quietAcceleration.Aggregate(Vector3D.Zero, (sum, x) => sum + x) / quietAcceleration.Count;
                accelerationNoise = Math.Sqrt(quietAcceleration.Average(x => Math.Pow((x - average).Length, 2)));
            }
        }
        else quietAcceleration.Clear();
        var motion = new DeviceMotion(time, linearVector, world, filtered, angular, confidence);
        lastMotion = time;
        UpdateSnapshot(time, motion); return motion;
    }
    private void UpdateSnapshot(double time, DeviceMotion? motion = null)
    {
        var forward = orientation.Rotate(new(0, 1, 0));
        var heading = Math.Sqrt(forward.X * forward.X + forward.Y * forward.Y) > .1 ? Math.Atan2(forward.X, forward.Y) : (double?)null;
        snapshot = new(hasOrientation ? new(time, orientation, heading, frame, frame.Contains("native") ? .8 : .3) : null,
            motion ?? snapshot.Motion, bias, gyroNoise, accelerationNoise, calibrated,
            "Orientation är telefonens pose; den skapar ingen personposition. Bias kräver lugnt kalibreringsunderlag; yaw utan absolut referens driver.");
    }
}
