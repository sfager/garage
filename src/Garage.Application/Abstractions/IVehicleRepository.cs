using Garage.Domain.Entities;

namespace Garage.Application.Abstractions;

public interface IVehicleRepository : IRepository<Vehicle>
{
    /// <summary>Vehicles in the switcher: active ones only, oldest first.</summary>
    Task<IReadOnlyList<Vehicle>> ListActiveAsync(Guid householdId, CancellationToken cancellationToken = default);

    /// <summary>Everything the household owns, archived included — reports need these.</summary>
    Task<IReadOnlyList<Vehicle>> ListAllAsync(Guid householdId, CancellationToken cancellationToken = default);

    /// <summary>Loads a vehicle only if it belongs to the given household.</summary>
    Task<Vehicle?> GetForHouseholdAsync(Guid vehicleId, Guid householdId, CancellationToken cancellationToken = default);

    Task<bool> VinExistsAsync(string vin, Guid householdId, CancellationToken cancellationToken = default);
}
