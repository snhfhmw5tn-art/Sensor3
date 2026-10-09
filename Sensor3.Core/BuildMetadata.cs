using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sensor3.Contracts;

namespace Sensor3.Core;

public static class BuildMetadata
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static BuildInfo Load(Assembly assembly)
    {
        using var stream = assembly.GetManifestResourceStream("Sensor3.BuildInfo.json");
        return stream is null ? new() : JsonSerializer.Deserialize<BuildInfo>(stream, Options) ?? new();
    }

    public static IReadOnlyList<IterationInfo> LoadIterations(Assembly assembly)
    {
        using var stream = assembly.GetManifestResourceStream("Sensor3.Iterations.json")
            ?? throw new InvalidOperationException("Iterationsmanifest saknas.");
        var entries = JsonSerializer.Deserialize<IterationInfo[]>(stream, Options)
            ?? throw new InvalidOperationException("Iterationsmanifest är tomt.");
        Validate(entries);
        return entries;
    }

    public static void Validate(IReadOnlyList<IterationInfo> entries)
    {
        if (entries.Count != 18 || !entries.Select(x => x.Number).Order().SequenceEqual(Enumerable.Range(1, 18)))
            throw new InvalidOperationException("Manifestet måste innehålla iteration 01–18 exakt en gång.");
        if (entries.Any(x => x.Status == IterationStatus.Verified && x.VerifiedAtUtc is null))
            throw new InvalidOperationException("Verified kräver verifieringsdatum.");
    }

    public static string SwedishTime(DateTimeOffset? utc) => utc is null ? "Unknown" :
        TimeZoneInfo.ConvertTime(utc.Value, TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm"))
            .ToString("yyyy-MM-dd HH:mm:ss zzz");
}
