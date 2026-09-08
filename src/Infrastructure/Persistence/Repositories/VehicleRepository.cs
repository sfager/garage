using Garage.Application.Abstractions;
using Garage.Application.Vehicles;
using Garage.Domain.Entities;
using Garage.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Garage.Infrastructure.Persistence.Repositories;

public class HouseholdRepository(GarageDbContext context)
    : RepositoryBase<Household>(context), IHouseholdRepository;

public class VehicleRepository(GarageDbContext context)
    : RepositoryBase<Vehicle>(context), IVehicleRepository
{
    public async Task<IReadOnlyList<Vehicle>> ListActiveAsync(Guid householdId, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Where(v => v.HouseholdId == householdId && !v.IsArchived)
            .OrderBy(v => v.CreatedUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Vehicle>> ListAllAsync(Guid householdId, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Where(v => v.HouseholdId == householdId)
            .OrderBy(v => v.IsArchived)
            .ThenBy(v => v.CreatedUtc)
            .ToListAsync(cancellationToken);

    public Task<Vehicle?> GetForHouseholdAsync(Guid vehicleId, Guid householdId, bool noTracking = false,
        CancellationToken cancellationToken = default)
    {
        var query = noTracking ? Set.AsNoTracking() : Set;
        return query.FirstOrDefaultAsync(v => v.Id == vehicleId && v.HouseholdId == householdId, cancellationToken);
    }
    
    public Task<bool> VinExistsAsync(string vin, Guid householdId, CancellationToken cancellationToken = default) =>
        Set.AnyAsync(v => v.HouseholdId == householdId && v.Vin == vin, cancellationToken);

    public Task<bool> OwnsStoredFileAsync(string storageKey, Guid householdId, CancellationToken cancellationToken = default) =>
        Set.AnyAsync(
            v => v.HouseholdId == householdId
                 && (v.PhotoPath == storageKey || v.Documents.Any(d => d.StoragePath == storageKey)),
            cancellationToken);

    public async Task<VehicleDeletionImpact?> GetDeletionImpactAsync(
        Guid vehicleId,
        Guid householdId,
        CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Where(v => v.Id == vehicleId && v.HouseholdId == householdId)
            .Select(v => new VehicleDeletionImpact(
                v.Nickname,
                v.ServiceRecords.Count,
                v.FuelEntries.Count,
                v.OdometerReadings.Count,
                v.Trips.Count,
                v.Reminders.Count,
                v.Documents.Count,
                v.ServiceRecords.Sum(s => (decimal?)s.TotalCost) + v.FuelEntries.Sum(f => (decimal?)f.TotalCost) ?? 0m))
            .FirstOrDefaultAsync(cancellationToken);
}
