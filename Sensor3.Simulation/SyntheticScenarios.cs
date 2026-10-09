using Sensor3.Contracts;
namespace Sensor3.Simulation;
public enum SyntheticScenario { StraightWalking, SwingingArm, Pocket, PhoneRotation, SmallBodyTurn, SmoothCurve, Running, Stationary, Scanning, ForkliftDriving, ForkliftVibration, GpsGap, WifiNoise, SensorGap, NetworkDelay }
public static class SyntheticScenarios
{
    public static SensorRecording Create(SyntheticScenario scenario, double duration = 20)
    {
        if (!Enum.IsDefined(scenario) || !double.IsFinite(duration) || duration is < 5 or > 120) throw new ArgumentOutOfRangeException(nameof(scenario));
        var started = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero); var frames = new List<RecordedFrame>();
        var kinds = new[] { SensorKind.Gyroscope, SensorKind.RotationVector, SensorKind.Gravity, SensorKind.LinearAcceleration };
        var catalogue = kinds.Select(k => new SensorDescriptor(k.ToString(), "synthetic", k.ToString(), "SYNTHETIC TEST DATA", k, new(SensorReportingMode.Continuous), SensorStatus.Available)).ToArray();
        var truck = scenario is SyntheticScenario.ForkliftDriving or SyntheticScenario.ForkliftVibration or SyntheticScenario.GpsGap;
        var staticMotion = scenario is SyntheticScenario.Stationary or SyntheticScenario.Scanning || truck;
        var frequency = scenario == SyntheticScenario.Running ? 3 : 2; var length = scenario == SyntheticScenario.Running ? 1 : .7;
        var xy = new LocalPoint(0, 0); double previousPhone = 0;
        for (var i = 0; i <= duration * 50; i++)
        {
            var t = i * .02;
            if (scenario == SyntheticScenario.SensorGap && t is >= 8 and < 10)
            {
                if (i == 400) foreach (var kind in kinds) frames.Add(new(t, new(State: new(kind.ToString(), SensorStatus.Interrupted, "SYNTHETIC GAP"))));
                continue;
            }
            var heading = scenario == SyntheticScenario.SmallBodyTurn ? Math.PI / 9 * Math.Clamp((t - 5) / 1, 0, 1) : scenario == SyntheticScenario.SmoothCurve ? .05 * Math.Max(0, t - 4) : 0;
            var phone = heading + (scenario == SyntheticScenario.PhoneRotation ? Math.PI / 2 * Math.Clamp((t - 5) / 1, 0, 1) : scenario == SyntheticScenario.SwingingArm ? .4 * Math.Sin(2 * Math.PI * frequency * t) : scenario == SyntheticScenario.Scanning ? .8 * Math.Sin(2 * t) : 0);
            var q = QuaternionD.AxisAngle(Vector3D.Up, -phone);
            if (scenario == SyntheticScenario.Pocket) q = q * QuaternionD.AxisAngle(new(1, 0, 0), .6);
            var wave = staticMotion ? 0 : Math.Sin(2 * Math.PI * frequency * t);
            var horizontalAmplitude = scenario == SyntheticScenario.Running ? 1.4 : .8;
            var world = new Vector3D(horizontalAmplitude * wave * Math.Sin(heading), horizontalAmplitude * wave * Math.Cos(heading), (scenario == SyntheticScenario.Running ? 3.5 : 2) * wave);
            if (scenario == SyntheticScenario.ForkliftVibration) world = new(0, 0, .2 * Math.Sin(2 * Math.PI * 18 * t));
            if (scenario == SyntheticScenario.Scanning) world = new(.15 * Math.Sin(3.1 * t), .1 * Math.Sin(7.3 * t), .2 * Math.Sin(4.1 * t));
            var bodyGyro = q.Conjugate().Rotate(new(0, 0, i == 0 ? 0 : -(phone - previousPhone) / .02)); previousPhone = phone;
            var delay = scenario == SyntheticScenario.NetworkDelay ? .2 : 0;
            void Add(SensorKind kind, Vector3D vector, double? w = null, GroundTruth? truth = null)
            {
                var values = new List<SensorValue> { new("x", vector.X, kind == SensorKind.Gyroscope ? "rad/s" : kind == SensorKind.RotationVector ? "1" : "m/s²"), new("y", vector.Y, "SI"), new("z", vector.Z, "SI") };
                if (w is { } scalar) values.Add(new("w", scalar, "1"));
                frames.Add(new(t + delay, new(new(kind.ToString(), kind, values, (long)((1000 + t) * 1e9), null, started.AddSeconds(t + delay), SensorTimestampSource.AndroidElapsedRealtime, SensorQuality.Medium, "SYNTHETIC device/world")), truth));
            }
            Add(SensorKind.Gyroscope, bodyGyro); Add(SensorKind.RotationVector, new(q.X, q.Y, q.Z), q.W); Add(SensorKind.Gravity, q.Conjugate().Rotate(Vector3D.Up * 9.80665));
            if (!staticMotion) xy = new(xy.X + length * frequency * .02 * Math.Sin(heading), xy.Y + length * frequency * .02 * Math.Cos(heading));
            var activity = truck ? ActivityKind.Forklift : scenario == SyntheticScenario.Running ? ActivityKind.Running : scenario == SyntheticScenario.Scanning ? ActivityKind.Unknown : scenario == SyntheticScenario.Stationary ? ActivityKind.Stationary : ActivityKind.Walking;
            var speed = scenario == SyntheticScenario.ForkliftVibration ? 0 : 2;
            Add(SensorKind.LinearAcceleration, q.Conjugate().Rotate(world), truth: new(staticMotion ? 0 : (long)Math.Floor(t * frequency), truck ? speed * t : staticMotion ? 0 : length * frequency * t, truck ? Math.PI / 2 : heading, truck ? new(speed * t, 0) : xy, activity));
            if (truck && i % 50 == 0 && !(scenario == SyntheticScenario.GpsGap && t is >= 6 and <= 12)) frames.Add(new(t, new(Location: new(0, speed * t / 6371000 * 180 / Math.PI, 1, null, speed, 90, started.AddSeconds(t), false))));
            if (scenario == SyntheticScenario.WifiNoise && i % 50 == 0) frames.Add(new(t, new(Wifi: Enumerable.Range(1, 3).Select(n => new WifiObservation("SYNTHETIC", $"synthetic-ap-{n}", -55 + 20 * Math.Sin(t * n), 2412, started.AddSeconds(t), null, false)).ToArray())));
        }
        var carry = scenario == SyntheticScenario.Pocket ? CarryingKind.Pocket : scenario == SyntheticScenario.Scanning ? CarryingKind.Scanning : CarryingKind.Unknown;
        var recording = new SensorRecording(1, $"SYNTHETIC {scenario}", true, started, catalogue, frames.OrderBy(x => x.OffsetSeconds).ToArray(), KnownStartHeading: 0, StartPosition: new(0, 0), DeclaredForklift: truck, DeclaredCarrying: carry, GpsOrigin: truck ? new(0, 0) : null);
        RecordingCodec.Validate(recording); return recording;
    }
}
