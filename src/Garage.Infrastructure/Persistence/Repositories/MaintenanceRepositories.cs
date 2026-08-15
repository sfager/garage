using Garage.Application.Abstractions;
using Garage.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Garage.Infrastructure.Persistence.Repositories;

public class ReminderRepository(GarageDbContext context)
    : RepositoryBase<Reminder>(context), IReminderRepository
{
    public async Task<IReadOnlyList<Reminder>> ListActiveAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        await Set.Where(r => r.VehicleId == vehicleId && !r.IsDismissed)
            .OrderBy(r => r.Item)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Reminder>> ListAllAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        await Set.Where(r => r.VehicleId == vehicleId)
            .OrderBy(r => r.Item)
            .ToListAsync(cancellationToken);

    /// <summary>Joins through the vehicle so a reminder id alone cannot reach another household.</summary>
    public Task<Reminder?> GetForHouseholdAsync(Guid reminderId, Guid householdId, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(
            r => r.Id == reminderId && r.Vehicle!.HouseholdId == householdId,
            cancellationToken);
}

public class ServiceRecordRepository(GarageDbContext context)
    : RepositoryBase<ServiceRecord>(context), IServiceRecordRepository
{
    public async Task<IReadOnlyList<ServiceRecord>> ListForVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        await Set.Where(s => s.VehicleId == vehicleId)
            .Include(s => s.Items)
            .Include(s => s.Receipts)
            .OrderByDescending(s => s.Date)
            .ThenByDescending(s => s.Odometer)
            .ToListAsync(cancellationToken);

    public Task<ServiceRecord?> GetForHouseholdAsync(Guid recordId, Guid householdId, CancellationToken cancellationToken = default) =>
        Set.Include(s => s.Items)
            .Include(s => s.Receipts)
            .FirstOrDefaultAsync(
                s => s.Id == recordId && s.Vehicle!.HouseholdId == householdId,
                cancellationToken);

    public async Task<IReadOnlyList<string>> ListShopsAsync(Guid householdId, CancellationToken cancellationToken = default) =>
        await Set.Where(s => s.Vehicle!.HouseholdId == householdId && s.Shop != null)
            .Select(s => s.Shop!)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(cancellationToken);
}
