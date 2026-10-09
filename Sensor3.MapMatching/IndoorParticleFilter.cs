using Sensor3.Contracts;
namespace Sensor3.MapMatching;
public sealed class IndoorParticleFilter
{
    private readonly int count;
    private readonly Random random;
    private LocalPoint? lastRaw;
    private bool lost;
    private List<LocalPoint> particles = [];
    public IndoorParticleFilter(int count = 256, int? seed = null) { if (count is < 32 or > 2048) throw new ArgumentOutOfRangeException(nameof(count)); this.count = count; random = seed is { } value ? new(value) : new(); }
    public void Reset() { lastRaw = null; particles.Clear(); lost = false; }
    public static void Validate(IndoorMap map)
    {
        if (map.Features is null || map.Features.Count > 2000 || map.Features.Any(f => f is null || f.A is null || f.B is null || !Enum.IsDefined(f.Kind) || !double.IsFinite(f.A.X) || !double.IsFinite(f.A.Y) || !double.IsFinite(f.B.X) || !double.IsFinite(f.B.Y) || f.Name is null || f.Name.Length > 128)) throw new InvalidDataException("Ogiltig eller för stor lagerkarta.");
    }
    private static bool Inside(LocalPoint p, MapFeature f) => p.X >= Math.Min(f.A.X, f.B.X) && p.X <= Math.Max(f.A.X, f.B.X) && p.Y >= Math.Min(f.A.Y, f.B.Y) && p.Y <= Math.Max(f.A.Y, f.B.Y);
    private static double Cross(LocalPoint a, LocalPoint b, LocalPoint c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
    private static bool Intersects(LocalPoint a, LocalPoint b, LocalPoint c, LocalPoint d) => Math.Max(a.X, b.X) >= Math.Min(c.X, d.X) && Math.Max(c.X, d.X) >= Math.Min(a.X, b.X) && Math.Max(a.Y, b.Y) >= Math.Min(c.Y, d.Y) && Math.Max(c.Y, d.Y) >= Math.Min(a.Y, b.Y) && Cross(a, b, c) * Cross(a, b, d) <= 0 && Cross(c, d, a) * Cross(c, d, b) <= 0;
    public static bool Allowed(LocalPoint from, LocalPoint to, IndoorMap map)
    {
        foreach (var f in map.Features)
        {
            if (f.Kind == MapFeatureKind.Wall && Intersects(from, to, f.A, f.B)) return false;
            if (f.Kind is not (MapFeatureKind.Rack or MapFeatureKind.Blocked)) continue;
            if (Inside(to, f) || Inside(from, f)) return false;
            var corners = new[] { f.A, new LocalPoint(f.A.X, f.B.Y), f.B, new LocalPoint(f.B.X, f.A.Y) };
            for (var i = 0; i < 4; i++) if (Intersects(from, to, corners[i], corners[(i + 1) % 4])) return false;
        }
        return true;
    }
    private static double SegmentDistance(LocalPoint p, MapFeature f)
    {
        var dx = f.B.X - f.A.X; var dy = f.B.Y - f.A.Y; var length = dx * dx + dy * dy;
        var t = length == 0 ? 0 : Math.Clamp(((p.X - f.A.X) * dx + (p.Y - f.A.Y) * dy) / length, 0, 1);
        return Math.Sqrt(Math.Pow(p.X - f.A.X - t * dx, 2) + Math.Pow(p.Y - f.A.Y - t * dy, 2));
    }
    private double Noise() => Math.Sqrt(-2 * Math.Log(Math.Max(1e-12, random.NextDouble()))) * Math.Cos(2 * Math.PI * random.NextDouble());
    public MatchedPosition Update(LocalPoint? raw, double uncertainty, IndoorMap map, bool truck = false)
    {
        if (raw is null || !double.IsFinite(raw.X) || !double.IsFinite(raw.Y) || !double.IsFinite(uncertainty) || uncertainty < 0 || map.Features.Count == 0) { Reset(); return new(null, 0, 0, "Unknown: position/geometri saknas."); }
        if (lost) return new(null, 0, 0, "Unknown: kartmatchningen förlorad. Importera/bekräfta kartan för ny start.");
        var sigma = Math.Clamp(uncertainty, .2, 10);
        if (particles.Count == 0)
        {
            for (var i = 0; i < count; i++) { var p = new LocalPoint(raw.X + Noise() * sigma, raw.Y + Noise() * sigma); if (Allowed(p, p, map)) particles.Add(p); }
            lastRaw = raw;
        }
        var dx = raw.X - lastRaw!.X; var dy = raw.Y - lastRaw.Y;
        var routes = map.Features.Where(f => f.Kind == (truck ? MapFeatureKind.TruckRoad : MapFeatureKind.Aisle)).ToArray();
        var candidates = new List<(LocalPoint Point, double Weight)>();
        foreach (var p in particles)
        {
            var next = new LocalPoint(p.X + dx + Noise() * .1, p.Y + dy + Noise() * .1);
            if (!Allowed(p, next, map)) continue;
            var error = Math.Pow(next.X - raw.X, 2) + Math.Pow(next.Y - raw.Y, 2);
            var route = routes.Length == 0 ? 0 : routes.Min(f => SegmentDistance(next, f));
            var weight = Math.Exp(-error / (2 * sigma * sigma) - route * route / 2); if (weight > 1e-15) candidates.Add((next, weight));
        }
        lastRaw = raw;
        if (candidates.Count == 0) { lost = true; particles.Clear(); return new(null, 0, 0, "Unknown: alla partiklar förkastade. Bekräfta karta och ny startreferens."); }
        var sum = candidates.Sum(x => x.Weight); var best = candidates.MaxBy(x => x.Weight).Point;
        particles.Clear();
        for (var i = 0; i < count; i++) { var choice = random.NextDouble() * sum; foreach (var candidate in candidates) { choice -= candidate.Weight; if (choice <= 0) { particles.Add(candidate.Point); break; } } }
        // A mean can lie inside a rack even if all samples are valid. Show a valid sample instead.
        return new(best, Math.Min(.7, candidates.Count / (double)count), candidates.Count, "Research-partikelfilter: rörelsedelta, kartbegränsningar, råpositions-/gånglikelihood och resampling. Ingen kalibrerad noggrannhet.");
    }
}
