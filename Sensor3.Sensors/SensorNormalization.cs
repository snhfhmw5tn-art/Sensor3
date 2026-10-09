using Sensor3.Contracts;

namespace Sensor3.Sensors;

public static class SensorNormalization
{
    public const double StandardGravity = 9.80665;
    public static double DegreesToRadians(double value) => value * Math.PI / 180;
    public static double GToMetersPerSecondSquared(double value) => value * StandardGravity;
    public static double MicroteslaToTesla(double value) => value * 1e-6;
    public static double HectopascalToPascal(double value) => value * 100;
    public static SensorQuality AndroidQuality(int accuracy) => accuracy switch { 0 => SensorQuality.Unreliable, 1 => SensorQuality.Low, 2 => SensorQuality.Medium, 3 => SensorQuality.High, _ => SensorQuality.Unknown };
    public static int AndroidSamplingPeriodMicroseconds(double frequencyHz)
    {
        new SensorSamplingOptions { FrequencyHz = frequencyHz }.Validate();
        return (int)Math.Ceiling(1_000_000 / frequencyHz);
    }
    public static uint WindowsReportIntervalMilliseconds(double frequencyHz, uint minimumInterval)
    {
        new SensorSamplingOptions { FrequencyHz = frequencyHz }.Validate();
        return Math.Max(minimumInterval, (uint)Math.Ceiling(1000 / frequencyHz));
    }
    public static IReadOnlyList<SensorValue> AndroidValues(int type, IReadOnlyList<float> values)
    {
        // Preserve every native channel, including bias/heading-accuracy channels. Unknown units stay native.
        var unit = type switch { 1 or 9 or 10 or 35 => "m/s²", 4 or 16 => "rad/s", 3 or 36 => "rad", 2 or 14 => "T", 6 => "Pa", 5 => "lx", 8 => "m", 7 or 13 => "K", 12 => "1", 21 => "Hz", 18 or 19 => "count", 11 or 15 or 20 => "1", _ => "native" };
        var result = new SensorValue[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            var value = (double)values[i];
            if (type is 2 or 14) value = MicroteslaToTesla(value);
            if (type == 6) value = HectopascalToPascal(value);
            if (type == 8) value /= 100;
            if (type is 3 or 36) value = DegreesToRadians(value);
            if (type is 7 or 13) value += 273.15;
            if (type == 12) value /= 100;
            if (type == 21) value /= 60;
            var name = i < 3 && type is 1 or 2 or 4 or 9 or 10 or 11 or 14 or 15 or 16 or 20 or 35 ? new[] { "x", "y", "z" }[i] : $"value{i}";
            if (type is 11 or 15 or 20 && i == 3) name = "w";
            var channelUnit = type is 11 or 20 && i == 4 ? "rad" : unit;
            result[i] = new(name, value, channelUnit);
        }
        return result;
    }
    public static IReadOnlyList<SensorDescriptor> CompleteCatalogue(IEnumerable<SensorDescriptor> detected)
    {
        var result = detected.ToList();
        if (result.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != result.Count) throw new InvalidDataException("Duplicerade sensor-ID:n.");
        foreach (var kind in Enum.GetValues<SensorKind>().Where(x => x != SensorKind.Other))
            if (!result.Any(x => x.Kind == kind)) result.Add(new("unsupported:" + kind, "", kind.ToString(), "", kind, new(SensorReportingMode.Continuous), SensorStatus.Unsupported, "Ingen sensor exponerad av operativsystemet."));
        return result.AsReadOnly();
    }
}
