using Garage.Domain.Entities;
using Garage.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Garage.Infrastructure.Persistence.Repositories;

public class PushSubscriptionRepository(GarageDbContext context)
    : RepositoryBase<PushSubscription>(context), IPushSubscriptionRepository
{
    public async Task<IReadOnlyList<PushSubscription>> ListForHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default) =>
        await Set.Where(s => s.HouseholdId == householdId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PushSubscription>> ListAllAsync(CancellationToken cancellationToken = default) =>
        await Set.ToListAsync(cancellationToken);

    public Task<PushSubscription?> GetByEndpointAsync(string endpoint, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(s => s.Endpoint == endpoint, cancellationToken);
}

public class SentNotificationRepository(GarageDbContext context)
    : RepositoryBase<SentNotification>(context), ISentNotificationRepository
{
    public async Task<IReadOnlySet<string>> ListSentKeysAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        var keys = await Set
            .Where(n => n.HouseholdId == householdId)
            .Select(n => n.SubjectKey)
            .ToListAsync(cancellationToken);

        return keys.ToHashSet(StringComparer.Ordinal);
    }

    public async Task PruneAsync(DateTimeOffset before, CancellationToken cancellationToken = default) =>
        await Set.Where(n => n.SentUtc < before).ExecuteDeleteAsync(cancellationToken);
}

/// <summary>
/// The sweep runs on a timer with no signed-in user, so these queries take the household
/// id directly rather than resolving it from a principal.
/// </summary>
public class NotificationScanRepository(GarageDbContext context) : INotificationScanRepository
{
    public async Task<IReadOnlyList<Guid>> ListHouseholdIdsWithSubscriptionsAsync(CancellationToken cancellationToken = default) =>
        await context.PushSubscriptions
            .Select(s => s.HouseholdId)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<(Reminder Reminder, string VehicleNickname, int CurrentOdometer)>> ListActiveRemindersAsync(
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        var rows = await context.Reminders
            .Where(r => !r.IsDismissed && r.Vehicle!.HouseholdId == householdId && !r.Vehicle.IsArchived)
            .Select(r => new { Reminder = r, r.Vehicle!.Nickname, r.Vehicle.CurrentOdometer })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => (r.Reminder, r.Nickname, r.CurrentOdometer))];
    }

    public async Task<IReadOnlyList<(Document Document, string VehicleNickname)>> ListExpiringDocumentsAsync(
        Guid householdId,
        DateOnly through,
        CancellationToken cancellationToken = default)
    {
        var rows = await context.Documents
            .Where(d => d.Vehicle!.HouseholdId == householdId
                        && !d.Vehicle.IsArchived
                        && d.Type != Domain.DocumentType.Receipt
                        && d.ExpiresOn != null
                        && d.ExpiresOn <= through)
            .Select(d => new { Document = d, d.Vehicle!.Nickname })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => (r.Document, r.Nickname))];
    }
}
