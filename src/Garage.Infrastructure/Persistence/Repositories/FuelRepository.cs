using Garage.Domain.Entities;
using Garage.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Garage.Infrastructure.Persistence.Repositories;

public class FuelRepository(GarageDbContext context)
    : RepositoryBase<FuelEntry>(context), IFuelRepository
{
    public async Task<IReadOnlyList<FuelEntry>> ListForVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        await Set.Where(f => f.VehicleId == vehicleId)
            .OrderBy(f => f.Odometer)
            .ToListAsync(cancellationToken);

    public Task<FuelEntry?> GetForHouseholdAsync(Guid entryId, Guid householdId, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(
            f => f.Id == entryId && f.Vehicle!.HouseholdId == householdId,
            cancellationToken);

    public async Task<IReadOnlyList<string>> ListStationsAsync(Guid householdId, CancellationToken cancellationToken = default) =>
        await Set.Where(f => f.Vehicle!.HouseholdId == householdId && f.Station != null)
            .Select(f => f.Station!)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(cancellationToken);

    public async Task<decimal> SumSpendAsync(Guid vehicleId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
        await Set.Where(f => f.VehicleId == vehicleId && f.Date >= from && f.Date <= to)
            .SumAsync(f => (decimal?)f.TotalCost, cancellationToken) ?? 0m;
}
