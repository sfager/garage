using Garage.Domain.Entities;

namespace Garage.Domain.Repositories;

public interface IVehicleRepository : IRepository<Vehicle>
{
    /// <summary>Vehicles in the switcher: active ones only, oldest first.</summary>
    Task<IReadOnlyList<Vehicle>> ListActiveAsync(Guid householdId, CancellationToken cancellationToken = default);

    /// <summary>Everything the household owns, archived included — reports need these.</summary>
    Task<IReadOnlyList<Vehicle>> ListAllAsync(Guid householdId, CancellationToken cancellationToken = default);

    /// <summary>Loads a vehicle only if it belongs to the given household.</summary>
    Task<Vehicle?> GetForHouseholdAsync(Guid vehicleId, Guid householdId, CancellationToken cancellationToken = default);

    Task<bool> VinExistsAsync(string vin, Guid householdId, CancellationToken cancellationToken = default);

    /// <summary>Counts what a delete would destroy, for the confirmation dialog (story V-4).</summary>
    Task<VehicleDeletionImpact?> GetDeletionImpactAsync(Guid vehicleId, Guid householdId, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when the stored file — a vehicle photo, a document or a receipt — hangs off
    /// a vehicle this household owns. Guards the file endpoint so an unguessed key is
    /// not the only thing standing between one household and another's paperwork.
    /// </summary>
    Task<bool> OwnsStoredFileAsync(string storageKey, Guid householdId, CancellationToken cancellationToken = default);
}
