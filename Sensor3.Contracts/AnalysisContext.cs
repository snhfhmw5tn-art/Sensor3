namespace Sensor3.Contracts;
// Operator settings, not measurements or ground truth. Null fields mean unchanged.
public sealed record AnalysisContext(bool? DeclaredForklift = null, CarryingKind? DeclaredCarrying = null,
    double? KnownStartHeading = null, LocalPoint? StartPosition = null, LocalPoint? GpsOrigin = null, bool ResetCounters = false);
public interface IAnalysisContextSource
{
    event Action<AnalysisContext>? ContextChanged;
    AnalysisContext GetAnalysisContext();
}
public sealed record RemoteAnalysisSnapshot(StepSnapshot Steps, ActivityEstimate Activity, CarryingEstimate Carrying, HumanHeading Heading,
    FusionSnapshot Fusion, PositionSnapshot Position, VehicleSnapshot Vehicle, RadioPosition Radio);
public interface IRealtimeAnalysisSource
{
    RemoteAnalysisSnapshot? GetAnalysis(Guid sessionId);
    void ConfigureAnalysis(Guid sessionId, AnalysisContext context);
    void ConfigureRadioMap(Guid sessionId, string json);
}
