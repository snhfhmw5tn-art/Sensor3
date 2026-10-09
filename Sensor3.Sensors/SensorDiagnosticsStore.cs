using Sensor3.Contracts;

namespace Sensor3.Sensors;

// Bounded, per-client store. No native API, browser collection or generated measurement data.
public sealed class SensorDiagnosticsStore : ISensorDiagnosticsReceiver
{
    private readonly object gate = new();
    private readonly TimeProvider clock;
    private readonly SensorDiagnosticsOptions options;
    private readonly Dictionary<string, Entry> entries = [];
    private DiagnosticsClient? client;
    public SensorDiagnosticsOptions Options => options;
    private sealed class Entry(SensorDescriptor descriptor)
    {
        public readonly SensorDescriptor Descriptor = descriptor;
        public SensorDiagnosticStatus Status = descriptor.Status == SensorStatus.Running ? SensorDiagnosticStatus.Initializing : MapStatus(descriptor.Status);
        public SensorPermission Permission = PermissionFor(descriptor);
        public SensorReading? Latest;
        public SensorValue[] Filtered = [];
        public readonly Queue<DiagnosticSample> History = new();
        public long Count;
        public long Rejected;
        public long LastArrival;
        public long? StartedAt;
        public double? FirstTime;
        public double? LastTime;
        public double? LastChartTime;
        public double? RequestedFrequency;
        public string? Error = descriptor.Status == SensorStatus.Error ? descriptor.Detail : null;
    }
    public SensorDiagnosticsStore(SensorDiagnosticsOptions? options = null, TimeProvider? timeProvider = null)
    {
        this.options = options ?? new();
        this.options.Validate();
        clock = timeProvider ?? TimeProvider.System;
    }
    public void SetClient(DiagnosticsClient value, IReadOnlyList<SensorDescriptor> catalogue)
    {
        if (!Enum.IsDefined(value.Platform) || string.IsNullOrWhiteSpace(value.Id)) throw new ArgumentException("Native klientidentitet krävs.");
        if (catalogue.Count > 256 || catalogue.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != catalogue.Count)
            throw new ArgumentException("Ogiltig sensorkatalog.");
        lock (gate)
        {
            client = value;
            entries.Clear();
            foreach (var descriptor in catalogue) entries.Add(descriptor.Id, new(descriptor)
            { StartedAt = descriptor.Status == SensorStatus.Running ? clock.GetTimestamp() : null });
        }
    }
    public void BeginSession(IReadOnlyCollection<string> ids, double frequencyHz)
    {
        new SensorSamplingOptions { FrequencyHz = frequencyHz }.Validate();
        lock (gate)
        {
            var selected = ids.Select(id => entries.TryGetValue(id, out var entry) ? entry : throw new ArgumentException("Okänd sensor.")).ToArray();
            foreach (var entry in selected)
            {
                entry.Latest = null; entry.Filtered = []; entry.History.Clear();
                entry.Count = entry.Rejected = 0;
                entry.FirstTime = entry.LastTime = entry.LastChartTime = null;
                entry.Status = SensorDiagnosticStatus.Initializing;
                entry.StartedAt = clock.GetTimestamp();
                entry.RequestedFrequency = frequencyHz;
                entry.Error = null;
            }
        }
    }
    public void SetState(SensorState state)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(state.SensorId, out var entry)) return;
            entry.Status = state.Status == SensorStatus.Running && entry.Latest is null ? SensorDiagnosticStatus.Initializing : MapStatus(state.Status);
            if (state.Status == SensorStatus.Running) entry.StartedAt ??= clock.GetTimestamp();
            if (state.Status is SensorStatus.PermissionDenied or SensorStatus.PermissionRequired)
                entry.Permission = state.Status == SensorStatus.PermissionDenied ? SensorPermission.Denied : SensorPermission.Required;
            entry.Error = state.Status is SensorStatus.Error or SensorStatus.Interrupted ? state.Detail : null;
        }
    }
    public void Receive(SensorReading reading)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(reading.SensorId, out var entry) || entry.Status is SensorDiagnosticStatus.Unsupported or SensorDiagnosticStatus.PermissionDenied or SensorDiagnosticStatus.PermissionRequired or SensorDiagnosticStatus.Inactive or SensorDiagnosticStatus.Error) return;
            var seconds = reading.TimestampSource switch
            {
                SensorTimestampSource.AndroidElapsedRealtime when reading.MonotonicTimestampNanoseconds is >= 0 => reading.MonotonicTimestampNanoseconds.Value / 1e9,
                SensorTimestampSource.WindowsUtc when reading.TimestampUtc is { } utc => (utc - DateTimeOffset.UnixEpoch).TotalSeconds,
                _ => double.NaN
            };
            if (reading.Kind != entry.Descriptor.Kind || !double.IsFinite(seconds) || reading.Values.Count is 0 or > 64 ||
                (client?.Platform == ClientPlatform.Android && reading.TimestampSource != SensorTimestampSource.AndroidElapsedRealtime) ||
                (client?.Platform == ClientPlatform.Windows && reading.TimestampSource != SensorTimestampSource.WindowsUtc) ||
                reading.Values.Any(x => !double.IsFinite(x.Value)) || reading.Values.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != reading.Values.Count ||
                (entry.Latest is not null && reading.TimestampSource != entry.Latest.TimestampSource) ||
                (entry.LastTime is { } last && seconds <= last)) { entry.Rejected++; return; }
            var gap = entry.LastTime is { } previous ? seconds - previous : double.PositiveInfinity;
            var canFilter = CanFilter(entry.Descriptor);
            var alpha = 1 - Math.Exp(-gap / options.FilterTimeConstant.TotalSeconds);
            var old = entry.Filtered.ToDictionary(x => (x.Name, x.Unit), x => x.Value);
            entry.Filtered = canFilter ? reading.Values.Where(x => x.Unit is "m/s²" or "rad/s" or "T" or "Pa" or "lx" or "m")
                .Select(x => new SensorValue(x.Name, gap <= options.StaleAfter.TotalSeconds && old.TryGetValue((x.Name, x.Unit), out var y) ? (1 - alpha) * y + alpha * x.Value : x.Value, x.Unit)).ToArray() : [];
            var copied = reading with { Values = reading.Values.ToArray() };
            entry.Latest = copied;
            entry.FirstTime ??= seconds;
            entry.LastTime = seconds;
            entry.LastArrival = clock.GetTimestamp();
            entry.Count++;
            entry.Status = SensorDiagnosticStatus.Active;
            entry.Error = null;
            if (entry.LastChartTime is null || seconds - entry.LastChartTime >= options.ChartInterval.TotalSeconds)
            {
                entry.History.Enqueue(new(seconds - entry.FirstTime.Value, copied.Values, entry.Filtered.ToArray()));
                while (entry.History.Count > options.HistoryCapacity) entry.History.Dequeue();
                entry.LastChartTime = seconds;
            }
        }
    }
    public SensorDiagnosticsSnapshot GetSnapshot()
    {
        lock (gate) return new(client, entries.Values.Select(entry =>
        {
            TimeSpan? age = entry.Latest is null ? null : clock.GetElapsedTime(entry.LastArrival);
            var status = entry.Status;
            if (entry.Descriptor.Capability.ReportingMode == SensorReportingMode.Continuous &&
                status is SensorDiagnosticStatus.Active or SensorDiagnosticStatus.Initializing &&
                (age ?? (entry.StartedAt is { } started ? clock.GetElapsedTime(started) : TimeSpan.Zero)) >= options.StaleAfter)
                status = SensorDiagnosticStatus.Stale;
            return new SensorDiagnostic(entry.Descriptor, Description(entry.Descriptor.Kind), status, entry.Permission,
                false, "Ingen aktiv analyskoppling registrerad i denna diagnostik", entry.Latest is null ? null : entry.Latest with { Values = entry.Latest.Values.ToArray() },
                entry.Filtered.ToArray(), entry.History.Select(x => x with { Raw = x.Raw.ToArray(), Filtered = x.Filtered.ToArray() }).ToArray(),
                entry.Count, entry.Rejected, entry.Count < 2 ? null : (entry.Count - 1) / (entry.LastTime - entry.FirstTime),
                entry.RequestedFrequency, age, entry.Error);
        }).ToArray());
    }
    public static SensorDiagnosticStatus MapStatus(SensorStatus status) => status switch
    {
        SensorStatus.Available => SensorDiagnosticStatus.Available, SensorStatus.Unsupported => SensorDiagnosticStatus.Unsupported,
        SensorStatus.PermissionRequired => SensorDiagnosticStatus.PermissionRequired, SensorStatus.PermissionDenied => SensorDiagnosticStatus.PermissionDenied,
        SensorStatus.Running => SensorDiagnosticStatus.Active, SensorStatus.Stopped => SensorDiagnosticStatus.Inactive,
        SensorStatus.Interrupted => SensorDiagnosticStatus.Stale, _ => SensorDiagnosticStatus.Error
    };
    private static SensorPermission PermissionFor(SensorDescriptor descriptor) => descriptor.Status switch
    {
        SensorStatus.PermissionDenied => SensorPermission.Denied, SensorStatus.PermissionRequired => SensorPermission.Required,
        SensorStatus.Unsupported or SensorStatus.Error => SensorPermission.Unknown,
        _ => descriptor.Capability.RequiresPermission ? SensorPermission.Granted : SensorPermission.NotRequired
    };
    private static bool CanFilter(SensorDescriptor descriptor) => descriptor.Capability.ReportingMode == SensorReportingMode.Continuous &&
        descriptor.Kind is SensorKind.Accelerometer or SensorKind.Gyroscope or SensorKind.Gravity or SensorKind.LinearAcceleration or SensorKind.Magnetometer or SensorKind.Barometer or SensorKind.Light or SensorKind.Altimeter;
    private static string Description(SensorKind kind) => kind switch
    {
        SensorKind.Accelerometer => "Acceleration inklusive gravitation", SensorKind.Gyroscope => "Vinkelhastighet runt enhetens axlar",
        SensorKind.Gravity => "Operativsystemets gravitationsvektor", SensorKind.LinearAcceleration => "Acceleration med OS-beräknad gravitation borttagen",
        SensorKind.Magnetometer => "Magnetfält i enhetens referensram", SensorKind.RotationVector => "Operativsystemets rotationsrepresentation",
        SensorKind.Orientation => "Orientering i källans native referensram", SensorKind.Barometer => "Lufttryck",
        SensorKind.StepDetector => "Native steghändelser", SensorKind.StepCounter => "Native kumulativ stegräkning",
        SensorKind.Light => "Belysningsstyrka", SensorKind.Proximity => "Närvaro eller avstånd nära enheten",
        SensorKind.Altimeter => "Native relativ höjdförändring", SensorKind.Activity => "Operativsystemets aktivitetsklassificering",
        SensorKind.HingeAngle => "Enhetens gångjärnsvinkel", _ => "Övrig OS-exponerad sensor; kanalernas native enheter bevaras"
    };
}
