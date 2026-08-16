using Garage.Domain.Common;

namespace Garage.Domain.Entities;

/// <summary>
/// One browser that has agreed to receive notifications. A person may have several —
/// a phone and a laptop — so the household can hold many, and each is removed when the
/// browser reports it has expired.
/// </summary>
public class PushSubscription : Entity
{
    private PushSubscription() { }

    public PushSubscription(Guid householdId, string userId, string endpoint, string p256dh, string auth)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new DomainException("A push subscription needs an endpoint.");
        }

        if (string.IsNullOrWhiteSpace(p256dh) || string.IsNullOrWhiteSpace(auth))
        {
            throw new DomainException("A push subscription needs its encryption keys.");
        }

        HouseholdId = householdId;
        UserId = userId;
        Endpoint = endpoint;
        P256dh = p256dh;
        Auth = auth;
    }

    public Guid HouseholdId { get; private set; }

    /// <summary>Which person's browser this is, so a sign-out can drop just theirs.</summary>
    public string UserId { get; private set; } = string.Empty;

    /// <summary>The push service URL the browser gave us. Unique per browser install.</summary>
    public string Endpoint { get; private set; } = string.Empty;

    public string P256dh { get; private set; } = string.Empty;
    public string Auth { get; private set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUsedUtc { get; private set; }

    public void MarkUsed(DateTimeOffset when) => LastUsedUtc = when;
}
