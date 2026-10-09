namespace Sensor3.Contracts;
public enum MapFeatureKind { Wall, Rack, Aisle, TruckRoad, Blocked, Anchor }
public sealed record MapFeature(MapFeatureKind Kind, LocalPoint A, LocalPoint B, string Name);
public sealed record IndoorMap(IReadOnlyList<MapFeature> Features);
public sealed record MatchedPosition(LocalPoint? Point, double Confidence, int SurvivingParticles, string Detail);
public sealed record GeoCalibration(double OriginLatitude, double OriginLongitude, double LocalNorthBearingDegrees)
{
    public (double Latitude, double Longitude) Convert(LocalPoint point)
    {
        if (!double.IsFinite(OriginLatitude) || OriginLatitude is < -85 or > 85 || !double.IsFinite(OriginLongitude) || OriginLongitude is < -180 or > 180 || !double.IsFinite(LocalNorthBearingDegrees) || !double.IsFinite(point.X) || !double.IsFinite(point.Y)) throw new ArgumentOutOfRangeException(nameof(point));
        var yaw = LocalNorthBearingDegrees * Math.PI / 180;
        var east = point.X * Math.Cos(yaw) + point.Y * Math.Sin(yaw); var north = -point.X * Math.Sin(yaw) + point.Y * Math.Cos(yaw);
        return (OriginLatitude + north / 6371000 * 180 / Math.PI, OriginLongitude + east / (6371000 * Math.Cos(OriginLatitude * Math.PI / 180)) * 180 / Math.PI);
    }
}
