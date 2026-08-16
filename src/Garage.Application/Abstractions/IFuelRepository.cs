using Garage.Domain.Entities;

namespace Garage.Application.Abstractions;

public interface IFuelRepository : IRepository<FuelEntry>
{
    Task<IReadOnlyList<FuelEntry>> ListForVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    Task<FuelEntry?> GetForHouseholdAsync(Guid entryId, Guid householdId, CancellationToken cancellationToken = default);

    /// <summary>Story G-1: the station field remembers where fuel has been bought before.</summary>
    Task<IReadOnlyList<string>> ListStationsAsync(Guid householdId, CancellationToken cancellationToken = default);

    /// <summary>Fuel spend in a window, for the cost-per-mile and 30-day figures.</summary>
    Task<decimal> SumSpendAsync(Guid vehicleId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}
