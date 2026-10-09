using Sensor3.Contracts;
namespace Sensor3.Positioning;
public sealed record FingerprintOptions(int Neighbors = 3, int MinimumCommonAccessPoints = 3, double MaximumRssiErrorDb = 15, double FreshSeconds = 30);
public sealed class RadioFingerprintEstimator(FingerprintOptions? options = null, TimeProvider? timeProvider = null)
{
    private readonly FingerprintOptions settings = options ?? new();
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    public static void Validate(RadioMap map)
    {
        if (map.Fingerprints is null || map.Beacons is null) throw new InvalidDataException("Kartlistor saknas.");
        if (map.Fingerprints.Count > 2000 || map.Beacons.Count > 1000) throw new InvalidDataException("För stor radiokarta.");
        static bool Point(LocalPoint p) => double.IsFinite(p.X) && double.IsFinite(p.Y);
        foreach (var f in map.Fingerprints)
            if (f is null || f.Point is null || f.Observations is null || !Point(f.Point) || !double.IsFinite(f.Quality) || f.Quality is <= 0 or > 1 || f.Observations.Count is < 3 or > 512 || f.Observations.Any(x => x is null || string.IsNullOrWhiteSpace(x.Bssid) || x.Bssid.Length > 64 || !double.IsFinite(x.RssiDbm) || x.RssiDbm is < -127 or > 0 || !double.IsFinite(x.FrequencyMhz) || x.FrequencyMhz is < 2000 or > 7200)) throw new InvalidDataException("Ogiltigt fingerprint.");
        if (map.Beacons.Any(x => x is null || x.Point is null || string.IsNullOrWhiteSpace(x.Identifier) || x.Identifier.Length > 256 || !Point(x.Point) || !double.IsFinite(x.UncertaintyMeters) || x.UncertaintyMeters is < 1 or > 100)) throw new InvalidDataException("Ogiltigt BLE-ankare.");
    }
    private static RadioPosition Unknown(string detail) => new(null, 0, 0, "Unknown", detail);
    public RadioPosition Wifi(RadioMap map, IReadOnlyList<WifiObservation> observations)
    {
        if (settings.Neighbors < 1 || settings.MinimumCommonAccessPoints < 3 || settings.FreshSeconds <= 0) throw new InvalidOperationException("Ogiltig fingerprint-konfiguration.");
        var live = observations.Where(x => !x.IsCached && double.IsFinite(x.RssiDbm) && x.RssiDbm is >= -127 and <= 0 && (clock.GetUtcNow() - x.ReceivedAtUtc).TotalSeconds is >= -1 && (clock.GetUtcNow() - x.ReceivedAtUtc).TotalSeconds <= settings.FreshSeconds)
            .GroupBy(x => x.Bssid, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.MaxBy(y => y.RssiDbm)!, StringComparer.OrdinalIgnoreCase);
        var matches = new List<(WifiFingerprint Fingerprint, double Error)>();
        foreach (var f in map.Fingerprints)
        {
            var common = f.Observations.GroupBy(x => x.Bssid, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).Where(x => live.TryGetValue(x.Bssid, out var measured) && Math.Abs(measured.FrequencyMhz - x.FrequencyMhz) < 5).ToArray();
            if (common.Length < settings.MinimumCommonAccessPoints) continue;
            var error = Math.Sqrt(common.Average(x => Math.Pow(x.RssiDbm - live[x.Bssid].RssiDbm, 2))) + 5 * (1 - common.Length / (double)Math.Max(live.Count, f.Observations.Count));
            if (error <= settings.MaximumRssiErrorDb) matches.Add((f, error));
        }
        var nearest = matches.OrderBy(x => x.Error).Take(settings.Neighbors).ToArray();
        if (nearest.Length == 0) return Unknown("Minst tre färska gemensamma AP krävs; cache och frekvensbyte matchas inte.");
        var weights = nearest.Select(x => x.Fingerprint.Quality / Math.Pow(1 + x.Error, 2)).ToArray(); var sum = weights.Sum();
        var point = new LocalPoint(nearest.Select((x, i) => x.Fingerprint.Point.X * weights[i]).Sum() / sum, nearest.Select((x, i) => x.Fingerprint.Point.Y * weights[i]).Sum() / sum);
        var spread = Math.Sqrt(nearest.Select((x, i) => weights[i] * (Math.Pow(x.Fingerprint.Point.X - point.X, 2) + Math.Pow(x.Fingerprint.Point.Y - point.Y, 2))).Sum() / sum);
        return new(point, Math.Max(2, spread + nearest[0].Error * .3), Math.Clamp(nearest[0].Fingerprint.Quality * (1 - nearest[0].Error / 20), .1, .7), "WiFi WKNN", "RSSI-vector-matchning med heuristisk spridning; inte RSSI→avstånd eller kalibrerad sannolikhet.");
    }
    public RadioPosition Bluetooth(RadioMap map, IReadOnlyList<BluetoothObservation> observations)
    {
        var candidates = observations.Where(x => double.IsFinite(x.RssiDbm) && x.RssiDbm is >= -75 and <= 0 && (clock.GetUtcNow() - x.ReceivedAtUtc).TotalSeconds is >= -1 && (clock.GetUtcNow() - x.ReceivedAtUtc).TotalSeconds <= settings.FreshSeconds)
            .Select(x => (Observation: x, Anchor: map.Beacons.FirstOrDefault(b => b.Identifier.Equals(x.Identifier, StringComparison.OrdinalIgnoreCase)))).Where(x => x.Anchor is not null).OrderByDescending(x => x.Observation.RssiDbm).ToArray();
        if (candidates.Length == 0) return Unknown("Inget färskt känt BLE-ankare. Identifierare kan rotera.");
        if (candidates.Length > 1 && candidates[0].Observation.RssiDbm - candidates[1].Observation.RssiDbm < 6) return Unknown("BLE-ankare är tvetydiga; ingen säker korrigering.");
        var anchor = candidates[0].Anchor!; return new(anchor.Point, anchor.UncertaintyMeters, .4, "BLE anchor", "Operatörens närhetsradie; RSSI anger inte exakt avstånd.");
    }
}
