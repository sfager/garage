using Garage.Application.Abstractions;
using Garage.Domain.Common;
using Garage.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Garage.Infrastructure.Persistence.Repositories;

public abstract class RepositoryBase<T>(GarageDbContext context) : IRepository<T> where T : Entity
{
    protected GarageDbContext Context { get; } = context;
    protected DbSet<T> Set => Context.Set<T>();

    public virtual Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public virtual async Task AddAsync(T entity, CancellationToken cancellationToken = default) =>
        await Set.AddAsync(entity, cancellationToken);

    public virtual void Remove(T entity) => Set.Remove(entity);
}

public class HouseholdRepository(GarageDbContext context)
    : RepositoryBase<Household>(context), IHouseholdRepository;

public class VehicleRepository(GarageDbContext context)
    : RepositoryBase<Vehicle>(context), IVehicleRepository
{
    public async Task<IReadOnlyList<Vehicle>> ListActiveAsync(Guid householdId, CancellationToken cancellationToken = default) =>
        await Set.Where(v => v.HouseholdId == householdId && !v.IsArchived)
            .OrderBy(v => v.CreatedUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Vehicle>> ListAllAsync(Guid householdId, CancellationToken cancellationToken = default) =>
        await Set.Where(v => v.HouseholdId == householdId)
            .OrderBy(v => v.IsArchived)
            .ThenBy(v => v.CreatedUtc)
            .ToListAsync(cancellationToken);

    public Task<Vehicle?> GetForHouseholdAsync(Guid vehicleId, Guid householdId, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(v => v.Id == vehicleId && v.HouseholdId == householdId, cancellationToken);

    public Task<bool> VinExistsAsync(string vin, Guid householdId, CancellationToken cancellationToken = default) =>
        Set.AnyAsync(v => v.HouseholdId == householdId && v.Vin == vin, cancellationToken);
}
