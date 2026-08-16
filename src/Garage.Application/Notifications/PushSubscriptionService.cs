using Garage.Application.Abstractions;
using Garage.Domain.Entities;
using Garage.Domain.Repositories;

namespace Garage.Application.Notifications;

/// <summary>What the browser hands back after the user allows notifications.</summary>
public record PushSubscriptionRequest(string Endpoint, string P256dh, string Auth);

/// <summary>Registers and removes the browsers a household wants to be reached on.</summary>
public class PushSubscriptionService(
    IPushSubscriptionRepository subscriptions,
    ICurrentUser currentUser,
    IPushSender sender,
    IUnitOfWork unitOfWork)
{
    public string PublicKey => sender.PublicKey;

    public bool IsConfigured => sender.IsConfigured;

    /// <summary>
    /// Idempotent: a browser that re-subscribes reports the same endpoint, and its keys
    /// can rotate, so an existing row is replaced rather than duplicated.
    /// </summary>
    public async Task SubscribeAsync(PushSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);
        var userId = await currentUser.GetUserIdAsync(cancellationToken) ?? string.Empty;

        var existing = await subscriptions.GetByEndpointAsync(request.Endpoint, cancellationToken);
        if (existing is not null)
        {
            subscriptions.Remove(existing);
        }

        await subscriptions.AddAsync(
            new PushSubscription(householdId, userId, request.Endpoint, request.P256dh, request.Auth),
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UnsubscribeAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        var existing = await subscriptions.GetByEndpointAsync(endpoint, cancellationToken);
        if (existing is null)
        {
            return;
        }

        subscriptions.Remove(existing);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>How many browsers this household can currently be reached on.</summary>
    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);
        var all = await subscriptions.ListForHouseholdAsync(householdId, cancellationToken);
        return all.Count;
    }
}
