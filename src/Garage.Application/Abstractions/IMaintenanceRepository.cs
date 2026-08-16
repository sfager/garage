using Garage.Domain.Entities;

namespace Garage.Application.Abstractions;

public interface IReminderRepository : IRepository<Reminder>
{
    /// <summary>Live reminders for a vehicle — dismissed ones are excluded.</summary>
    Task<IReadOnlyList<Reminder>> ListActiveAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Everything including dismissed, for the reminders management list [1k].</summary>
    Task<IReadOnlyList<Reminder>> ListAllAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Loads a reminder only if its vehicle belongs to the given household.</summary>
    Task<Reminder?> GetForHouseholdAsync(Guid reminderId, Guid householdId, CancellationToken cancellationToken = default);
}

public interface IServiceRecordRepository : IRepository<ServiceRecord>
{
    /// <summary>Story S-6: history newest first, with items loaded for the summary line.</summary>
    Task<IReadOnlyList<ServiceRecord>> ListForVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    Task<ServiceRecord?> GetForHouseholdAsync(Guid recordId, Guid householdId, CancellationToken cancellationToken = default);

    /// <summary>Shops used before, so the wizard can offer them again (story L-2).</summary>
    Task<IReadOnlyList<string>> ListShopsAsync(Guid householdId, CancellationToken cancellationToken = default);

    /// <summary>Service spend in a window, which cost per mile counts alongside fuel.</summary>
    Task<decimal> SumSpendAsync(Guid vehicleId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>Dated service spend, for bucketing running costs by month.</summary>
    Task<IReadOnlyList<(DateOnly Date, decimal Cost)>> ListSpendAsync(Guid vehicleId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}
