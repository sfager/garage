using Garage.Domain.Entities;

namespace Garage.Application.Abstractions;

/// <summary>What a push notification says when it arrives.</summary>
public record PushMessage(string Title, string Body, string Url, string Tag);

/// <summary>
/// Sends a push to one browser. Implementations must not throw for a subscription the
/// push service has retired — they report it so the caller can drop it.
/// </summary>
public interface IPushSender
{
    /// <summary>The public VAPID key the browser needs in order to subscribe.</summary>
    string PublicKey { get; }

    bool IsConfigured { get; }

    Task<PushResult> SendAsync(PushSubscription subscription, PushMessage message, CancellationToken cancellationToken = default);
}

public enum PushResult
{
    Sent = 0,

    /// <summary>The push service says this subscription is gone; stop using it.</summary>
    SubscriptionExpired = 1,

    /// <summary>A transient failure — worth trying again on the next sweep.</summary>
    Failed = 2
}
