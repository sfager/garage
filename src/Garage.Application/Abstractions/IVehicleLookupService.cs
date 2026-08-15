using Garage.Application.Vehicles;

namespace Garage.Application.Abstractions;

/// <summary>
/// Turns a VIN or a plate into year, make, model and engine. Implementations must not
/// throw when the vehicle cannot be identified or the service is unreachable — they
/// return a failed result so the caller can fall back to manual entry (story V-1).
/// </summary>
public interface IVehicleLookupService
{
    Task<VehicleLookupResult> LookupAsync(LookupMethod method, string identifier, CancellationToken cancellationToken = default);
}
