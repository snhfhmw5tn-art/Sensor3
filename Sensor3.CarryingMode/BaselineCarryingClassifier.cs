using Sensor3.Contracts;

namespace Sensor3.CarryingMode;

public sealed record CarryingOptions
{
    public double WindowSeconds { get; init; } = 2;
    public double TransitionAccelerationChange { get; init; } = 8;
    public double HighRotationRadians { get; init; } = .7;
    public void Validate()
    {
        if (!double.IsFinite(WindowSeconds) || WindowSeconds is < 1 or > 5 || !double.IsFinite(TransitionAccelerationChange) || TransitionAccelerationChange <= 0 ||
            !double.IsFinite(HighRotationRadians) || HighRotationRadians <= 0) throw new ArgumentOutOfRangeException(nameof(CarryingOptions));
    }
}
public sealed class BaselineCarryingClassifier : ICarryingClassifier
{
    private sealed record Sample(double Time, Vector3D Raw, double Tilt, double Gyro, double Linear, double Vertical, double Horizontal);
    private readonly CarryingOptions options;
    private readonly List<Sample> samples = [];
    private (Vector3D Min, Vector3D Max)? previousWindow;
    private double lastWindow = double.NegativeInfinity, lastTime = double.NegativeInfinity;
    private bool? wasNear;
    private CarryingEstimate estimate = Unknown("Otillräckligt underlag.");
    public BaselineCarryingClassifier(CarryingOptions? options = null) { this.options = options ?? new(); this.options.Validate(); }
    public void Reset() { samples.Clear(); previousWindow = null; lastWindow = lastTime = double.NegativeInfinity; wasNear = null; estimate = Unknown("Nytt evidensfönster krävs."); }
    private static CarryingEstimate Unknown(string detail) => new(CarryingKind.Unknown, false, 0,
        Enum.GetValues<CarryingKind>().ToDictionary(x => x, x => x == CarryingKind.Unknown ? 1d : 0d), 0, 0, detail);
    public CarryingEstimate Update(DeviceMotion motion, DeviceOrientation? orientation, ActivityEstimate activity, CarryingContext context)
    {
        if (!double.IsFinite(motion.Seconds) || motion.Seconds <= lastTime) return estimate;
        if (double.IsFinite(lastTime) && motion.Seconds - lastTime > .5) Reset();
        lastTime = motion.Seconds;
        if (orientation is null || !motion.DeviceLinearAcceleration.IsFinite || !motion.WorldLinearAcceleration.IsFinite || motion.WorldAngularVelocity is not { IsFinite: true })
            return estimate = Unknown("Orientation/gyro saknas; bärposition kan inte identifieras säkert.");
        try { _ = orientation.DeviceToWorld.Normalized(); }
        catch (ArgumentException) { return estimate = Unknown("Ogiltig orientation."); }
        var gravity = orientation.DeviceToWorld.Conjugate().Rotate(Vector3D.Up * 9.80665);
        var tilt = Math.Acos(Math.Clamp(orientation.DeviceToWorld.Rotate(Vector3D.Up).Z, -1, 1));
        var world = motion.WorldLinearAcceleration;
        samples.Add(new(motion.Seconds, motion.DeviceLinearAcceleration + gravity, tilt, motion.WorldAngularVelocity.Value.Length,
            motion.DeviceLinearAcceleration.Length, Math.Abs(world.Z), Math.Sqrt(world.X * world.X + world.Y * world.Y)));
        samples.RemoveAll(x => x.Time < motion.Seconds - options.WindowSeconds); while (samples.Count > 600) samples.RemoveAt(0);
        if (samples.Count < 20 || samples[^1].Time - samples[0].Time < options.WindowSeconds * .9)
            return estimate = Unknown("Bärpositionsfönstret fylls.");
        if (motion.Seconds - lastWindow < options.WindowSeconds / 2) return estimate;
        lastWindow = motion.Seconds;
        var min = new Vector3D(samples.Min(x => x.Raw.X), samples.Min(x => x.Raw.Y), samples.Min(x => x.Raw.Z));
        var max = new Vector3D(samples.Max(x => x.Raw.X), samples.Max(x => x.Raw.Y), samples.Max(x => x.Raw.Z));
        var within = Sum(max - min);
        var cross = previousWindow is { } previous ? SumAbsolute(max - previous.Max) + SumAbsolute(min - previous.Min) : 0;
        previousWindow = (min, max);
        var transition = cross > options.TransitionAccelerationChange;
        var near = context.ProximityMeters is { } proximity && double.IsFinite(proximity) && proximity >= 0 ? proximity <= .01 : (bool?)null;
        var becameNear = near == true && wasNear == false; var becameFar = near == false && wasNear == true;
        if (near is not null) wasNear = near;
        var gyro = Math.Sqrt(samples.Average(x => x.Gyro * x.Gyro));
        var linear = Math.Sqrt(samples.Average(x => x.Linear * x.Linear));
        var vertical = samples.Average(x => x.Vertical); var horizontal = samples.Average(x => x.Horizontal);
        var tiltRange = samples.Max(x => x.Tilt) - samples.Min(x => x.Tilt);
        var tiltMean = samples.Average(x => x.Tilt);
        var scores = Enum.GetValues<CarryingKind>().ToDictionary(x => x, _ => .005);
        var kind = CarryingKind.Unknown; var confidence = .25; var detail = "Positioner kan överlappa i IMU; Unknown behålls vid svagt underlag.";
        if (context.Declared != CarryingKind.Unknown)
        { kind = context.Declared; confidence = .65; detail = "Deklarerad bärposition från operatör; ingen automatisk classifier-verifiering."; }
        else if (becameNear || becameFar)
        { kind = becameNear ? CarryingKind.PuttingAway : CarryingKind.PickingUp; confidence = .65; transition = true; detail = "Proximity-växling stöder övergång; objekttäckning är också möjlig."; }
        else if (near == true && context.LightLux is < 5 && activity.Kind is ActivityKind.Walking or ActivityKind.Running)
        { kind = CarryingKind.Pocket; confidence = .65; detail = "Täckt och mörk sensor under gång stöder fickhypotes; inte bevis på fickplacering."; }
        else if (gyro > options.HighRotationRadians && tiltRange < .3)
        { kind = CarryingKind.HandRotating; confidence = .65; detail = "Hög rotation med liten tiltändring. Människosväng kan inte uteslutas här."; }
        else if (gyro > .2 && tiltRange > .35 && activity.Kind is ActivityKind.Walking or ActivityKind.Running)
        { kind = CarryingKind.HandSwinging; confidence = .65; detail = "Periodisk gångevidens med varierande tilt stöder pendlande hand."; }
        else if (vertical > .5 && vertical > horizontal * 3 && tiltRange < .2)
        { kind = CarryingKind.VerticalSwinging; confidence = .65; detail = "Vertikal dominans; telefonlyft är ingen bekräftad personförflyttning."; }
        else if (linear < .16 && gyro < .05)
        { kind = CarryingKind.DeviceStationary; confidence = .8; detail = "Telefonen visar låg rörelseenergi. Hand/montering kan inte skiljas säkert."; }
        else if (gyro < .2 && tiltRange < .15 && activity.Kind is ActivityKind.Walking or ActivityKind.Running)
        { kind = tiltMean is > .4 and < 1.2 ? CarryingKind.Viewing : CarryingKind.HeldStable; confidence = .65; detail = "Stabil pose under gång; Viewing är en posehypotes, inte ögon-/blickmätning."; }
        scores[CarryingKind.Unknown] = 1 - confidence; scores[kind] = confidence;
        if (kind == CarryingKind.Unknown) { confidence = 0; scores[CarryingKind.Unknown] = 1; }
        var total = scores.Values.Sum(); var probabilities = scores.ToDictionary(x => x.Key, x => x.Value / total);
        estimate = new(kind, transition, confidence, probabilities, cross, within, detail + " Poäng normaliseras till heuristiska sannolikheter; inte statistiskt kalibrerade.");
        return estimate;
    }
    private static double Sum(Vector3D vector) => vector.X + vector.Y + vector.Z;
    private static double SumAbsolute(Vector3D vector) => Math.Abs(vector.X) + Math.Abs(vector.Y) + Math.Abs(vector.Z);
}
