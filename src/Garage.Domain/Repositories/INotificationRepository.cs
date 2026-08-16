using Garage.Domain.Entities;

namespace Garage.Domain.Repositories;

public interface IPushSubscriptionRepository : IRepository<PushSubscription>
{
    Task<IReadOnlyList<PushSubscription>> ListForHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default);

    /// <summary>Every subscription, for the sweep that runs across all households.</summary>
    Task<IReadOnlyList<PushSubscription>> ListAllAsync(CancellationToken cancellationToken = default);

    Task<PushSubscription?> GetByEndpointAsync(string endpoint, CancellationToken cancellationToken = default);
}

public interface ISentNotificationRepository : IRepository<SentNotification>
{
    /// <summary>The keys already notified, so a due point is only announced once.</summary>
    Task<IReadOnlySet<string>> ListSentKeysAsync(Guid householdId, CancellationToken cancellationToken = default);

    /// <summary>Drops notes older than the cutoff so the table does not grow without end.</summary>
    Task PruneAsync(DateTimeOffset before, CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads what the notification sweep needs without going through a signed-in user —
/// the sweep runs on a timer, for every household at once.
/// </summary>
public interface INotificationScanRepository
{
    Task<IReadOnlyList<Guid>> ListHouseholdIdsWithSubscriptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Every active reminder in a household, with the vehicle it belongs to.</summary>
    Task<IReadOnlyList<(Reminder Reminder, string VehicleNickname, int CurrentOdometer)>> ListActiveRemindersAsync(
        Guid householdId,
        CancellationToken cancellationToken = default);

    /// <summary>Documents expiring on or before a date, with their vehicle's name.</summary>
    Task<IReadOnlyList<(Document Document, string VehicleNickname)>> ListExpiringDocumentsAsync(
        Guid householdId,
        DateOnly through,
        CancellationToken cancellationToken = default);
}
