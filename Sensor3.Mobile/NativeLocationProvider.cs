using Sensor3.Contracts;
using Sensor3.Sensors;
using Microsoft.Extensions.Logging;

namespace Sensor3;

public sealed class NativeLocationProvider(UpdateSessionGuard guard, NativeObservationLifetime lifetime, ILogger<NativeLocationProvider> logger) : ILocationProvider
{
    public async Task<ObservationResult<LocationObservation>> GetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var linked = lifetime.Create(cancellationToken);
            cancellationToken = linked.Token;
            using var session = guard.BeginSession();
            var location = await MainThread.InvokeOnMainThreadAsync(() => Geolocation.Default.GetLocationAsync(new(GeolocationAccuracy.Best, TimeSpan.FromSeconds(15)), cancellationToken));
            if (location is null) return new(ObservationStatus.Error, null, "Ingen platsfix inom tidsgränsen.");
            return new(ObservationStatus.Available, RadioObservationRules.ValidateLocation(new(location.Latitude, location.Longitude, location.Accuracy,
                location.Altitude, location.Speed, location.Course, location.Timestamp.ToUniversalTime(), location.IsFromMockProvider)),
                "OS-platsfix; källan behöver inte vara enbart GPS. Mock-markeringen bevaras.");
        }
        catch (PermissionException) { return new(ObservationStatus.PermissionDenied, null, "Platsbehörighet nekad."); }
        catch (FeatureNotSupportedException) { return new(ObservationStatus.Unsupported, null); }
        catch (FeatureNotEnabledException) { return new(ObservationStatus.Disabled, null, "Aktivera platstjänster på enheten."); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) { logger.LogWarning(exception, "Native platsfix misslyckades"); return new(ObservationStatus.Error, null, exception.Message); }
    }
}
