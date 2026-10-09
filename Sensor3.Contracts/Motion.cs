namespace Sensor3.Contracts;

public readonly record struct Vector3D(double X, double Y, double Z)
{
    public static Vector3D Zero => new(0, 0, 0);
    public static Vector3D Up => new(0, 0, 1);
    public double Length => Math.Sqrt(Dot(this, this));
    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z);
    public Vector3D Normalized() => Length > 1e-12 && IsFinite ? this / Length : throw new ArgumentException("En ändlig, icke-noll vektor krävs.");
    public static double Dot(Vector3D a, Vector3D b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    public static Vector3D Cross(Vector3D a, Vector3D b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    public static Vector3D operator +(Vector3D a, Vector3D b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3D operator -(Vector3D a, Vector3D b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3D operator *(Vector3D a, double value) => new(a.X * value, a.Y * value, a.Z * value);
    public static Vector3D operator /(Vector3D a, double value) => a * (1 / value);
}
public readonly record struct QuaternionD(double X, double Y, double Z, double W)
{
    public static QuaternionD Identity => new(0, 0, 0, 1);
    public QuaternionD Normalized()
    {
        var length = Math.Sqrt(X * X + Y * Y + Z * Z + W * W);
        return double.IsFinite(length) && length > 1e-12 ? new(X / length, Y / length, Z / length, W / length) : throw new ArgumentException("Ogiltig kvaternion.");
    }
    public QuaternionD Conjugate() => new(-X, -Y, -Z, W);
    public static QuaternionD operator *(QuaternionD a, QuaternionD b) => new(a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
        a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X, a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W, a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);
    public Vector3D Rotate(Vector3D vector)
    {
        var q = Normalized(); var rotated = q * new QuaternionD(vector.X, vector.Y, vector.Z, 0) * q.Conjugate();
        return new(rotated.X, rotated.Y, rotated.Z);
    }
    public static QuaternionD AxisAngle(Vector3D axis, double radians)
    {
        if (!double.IsFinite(radians)) throw new ArgumentException("Ogiltig vinkel.");
        axis = axis.Normalized(); var sine = Math.Sin(radians / 2); return new(axis.X * sine, axis.Y * sine, axis.Z * sine, Math.Cos(radians / 2));
    }
    public static QuaternionD Align(Vector3D from, Vector3D to)
    {
        from = from.Normalized(); to = to.Normalized(); var dot = Math.Clamp(Vector3D.Dot(from, to), -1, 1);
        if (dot < -.999999) return AxisAngle(Vector3D.Cross(from, Math.Abs(from.X) < .8 ? new(1, 0, 0) : new(0, 1, 0)), Math.PI);
        var axis = Vector3D.Cross(from, to); return new QuaternionD(axis.X, axis.Y, axis.Z, 1 + dot).Normalized();
    }
    public double[,] Matrix()
    {
        var q = Normalized(); var x = q.X; var y = q.Y; var z = q.Z; var w = q.W;
        return new[,] { { 1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w) },
            { 2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w) },
            { 2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y) } };
    }
}
public sealed record DeviceOrientation(double Seconds, QuaternionD DeviceToWorld, double? DeviceHeadingRadians, string Frame, double Confidence);
public sealed record DeviceMotion(double Seconds, Vector3D DeviceLinearAcceleration, Vector3D WorldLinearAcceleration, Vector3D FilteredWorldAcceleration,
    Vector3D? WorldAngularVelocity, double Confidence);
public sealed record FusionSnapshot(DeviceOrientation? Orientation, DeviceMotion? Motion, Vector3D GyroBias, double? GyroNoiseRms,
    double? AccelerationNoiseRms, bool BiasCalibrated, string Detail);
public interface ISensorFusionSession
{
    FusionSnapshot GetFusionSnapshot();
    void BeginGyroCalibration();
}
