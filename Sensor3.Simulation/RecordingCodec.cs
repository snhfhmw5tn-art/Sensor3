using System.Globalization;
using System.Text;
using System.Text.Json;
using Sensor3.Contracts;
using Sensor3.Core;
namespace Sensor3.Simulation;
public static class RecordingCodec
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
    public static void Validate(SensorRecording recording)
    {
        if (recording.StartedAtUtc.Year is < 1990 or > 2100 || recording.Schema != 1 || string.IsNullOrWhiteSpace(recording.Name) || recording.Name.Length > 120 || recording.Frames is null || recording.Frames.Count > 60000 || recording.Catalogue is null || recording.Catalogue.Count > 256
            || recording.Catalogue.Any(x => x is null || string.IsNullOrWhiteSpace(x.Id) || x.Id.Length > 256 || !Enum.IsDefined(x.Kind)) || recording.Catalogue.Select(x => x.Id).Distinct().Count() != recording.Catalogue.Count || !Enum.IsDefined(recording.DeclaredCarrying) || recording.KnownStartHeading is { } h && !double.IsFinite(h) || recording.StartPosition is { } p && (!double.IsFinite(p.X) || !double.IsFinite(p.Y))) throw new InvalidDataException("Ogiltig inspelningsmetadata.");
        if (recording.RadioMap is { } map) Sensor3.Positioning.RadioFingerprintEstimator.Validate(map);
        double last = -1;
        foreach (var frame in recording.Frames)
        {
            if (frame is null || !double.IsFinite(frame.OffsetSeconds) || frame.OffsetSeconds < 0 || frame.OffsetSeconds < last || frame.OffsetSeconds > 86400) throw new InvalidDataException("Tidslinjen måste vara sorterad, 0–86400 s.");
            last = frame.OffsetSeconds; SensorEventValidation.Validate(frame.Event, recording.Catalogue);
            if (frame.Truth is { } truth && (truth.Steps < 0 || truth.DistanceMeters is { } d && (!double.IsFinite(d) || d < 0) || truth.HeadingRadians is { } heading && !double.IsFinite(heading) || truth.Position is { } point && (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) || truth.Activity is { } activity && !Enum.IsDefined(activity))) throw new InvalidDataException("Ogiltig ground truth.");
        }
    }
    public static string ToJson(SensorRecording recording) { Validate(recording); return JsonSerializer.Serialize(recording); }
    public static SensorRecording FromJson(string json)
    {
        if (json.Length > 16_000_000) throw new InvalidDataException("Inspelning över 16 MB nekas.");
        var recording = JsonSerializer.Deserialize<SensorRecording>(json, Options) ?? throw new InvalidDataException("Inspelning saknas."); Validate(recording); return recording;
    }
    private static string Quote(string text) => "\"" + text.Replace("\"", "\"\"") + "\"";
    public static string ToCsv(SensorRecording recording)
    {
        Validate(recording); var builder = new StringBuilder();
        builder.AppendLine($"Sensor3-CSV-1,{Quote(JsonSerializer.Serialize(recording with { Frames = [] }))},");
        builder.AppendLine("offsetSeconds,eventJson,groundTruthJson");
        foreach (var f in recording.Frames) builder.AppendLine($"{f.OffsetSeconds.ToString("R", CultureInfo.InvariantCulture)},{Quote(JsonSerializer.Serialize(f.Event))},{Quote(JsonSerializer.Serialize(f.Truth))}");
        return builder.ToString();
    }
    public static SensorRecording FromCsv(string csv)
    {
        if (csv.Length > 16_000_000) throw new InvalidDataException("CSV över 16 MB nekas.");
        var rows = Parse(csv); if (rows.Count < 2 || rows[0].Length != 3 || rows[0][0] != "Sensor3-CSV-1" || !rows[1].SequenceEqual(new[] { "offsetSeconds", "eventJson", "groundTruthJson" })) throw new InvalidDataException("Sensor3 CSV-schema saknas.");
        var header = FromJson(rows[0][1]); var frames = new List<RecordedFrame>();
        foreach (var row in rows.Skip(2))
        {
            if (row.Length != 3 || !double.TryParse(row[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)) throw new InvalidDataException("Ogiltig CSV-rad.");
            frames.Add(new(seconds, JsonSerializer.Deserialize<TelemetryEvent>(row[1], Options) ?? throw new InvalidDataException("Observation saknas."), JsonSerializer.Deserialize<GroundTruth>(row[2], Options)));
        }
        var result = header with { Frames = frames }; Validate(result); return result;
    }
    private static List<string[]> Parse(string csv)
    {
        var rows = new List<string[]>(); var fields = new List<string>(); var field = new StringBuilder(); bool quoted = false, closed = false;
        for (var i = 0; i < csv.Length; i++)
        {
            var c = csv[i];
            if (quoted)
            {
                if (c == '"') { if (i + 1 < csv.Length && csv[i + 1] == '"') { field.Append('"'); i++; } else { quoted = false; closed = true; } }
                else field.Append(c); continue;
            }
            if (c == '"' && field.Length == 0 && !closed) { quoted = true; continue; }
            if (c is ',' or '\r' or '\n')
            {
                fields.Add(field.ToString()); field.Clear(); closed = false;
                if (c == ',') continue;
                if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n') i++;
                rows.Add(fields.ToArray()); fields.Clear(); if (rows.Count > 60002) throw new InvalidDataException("För många CSV-rader.");
            }
            else { if (closed || c == '"') throw new InvalidDataException("Felaktig CSV-quoting."); field.Append(c); }
        }
        if (quoted) throw new InvalidDataException("Oavslutad CSV-sträng.");
        if (field.Length > 0 || fields.Count > 0) { fields.Add(field.ToString()); rows.Add(fields.ToArray()); }
        return rows;
    }
}
