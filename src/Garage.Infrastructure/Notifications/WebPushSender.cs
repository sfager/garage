using System.Text.Json;
using Garage.Application.Abstractions;
using Microsoft.Extensions.Logging;
using WebPush;
using DomainSubscription = Garage.Domain.Entities.PushSubscription;

namespace Garage.Infrastructure.Notifications;

public class VapidOptions
{
    /// <summary>Identifies this application to the push service. A mailto: or https: URL.</summary>
    public string Subject { get; set; } = "mailto:garage@example.com";

    public string? PublicKey { get; set; }
    public string? PrivateKey { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(PublicKey) && !string.IsNullOrWhiteSpace(PrivateKey);

    /// <summary>Generates a fresh VAPID key pair, for first run and for the setup docs.</summary>
    public static (string PublicKey, string PrivateKey) GenerateKeys()
    {
        var keys = VapidHelper.GenerateVapidKeys();
        return (keys.PublicKey, keys.PrivateKey);
    }
}

/// <summary>
/// Story S-5's delivery. Encrypts each payload to the browser's own keys per RFC 8291 —
/// the push service relays it without being able to read it — using the WebPush library
/// rather than hand-rolled cryptography.
/// </summary>
public class WebPushSender(VapidOptions options, ILogger<WebPushSender> logger) : IPushSender
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly WebPushClient _client = new();

    public string PublicKey => options.PublicKey ?? string.Empty;

    public bool IsConfigured => options.IsConfigured;

    public async Task<PushResult> SendAsync(
        DomainSubscription subscription,
        PushMessage message,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return PushResult.Failed;
        }

        var target = new WebPush.PushSubscription(subscription.Endpoint, subscription.P256dh, subscription.Auth);
        var vapid = new VapidDetails(options.Subject, options.PublicKey, options.PrivateKey);

        try
        {
            await _client.SendNotificationAsync(
                target,
                JsonSerializer.Serialize(message, Json),
                vapid,
                cancellationToken);

            return PushResult.Sent;
        }
        catch (WebPushException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Gone
                                              or System.Net.HttpStatusCode.NotFound)
        {
            // The browser was reinstalled, or the permission was revoked.
            logger.LogInformation("Push subscription has expired; dropping it.");
            return PushResult.SubscriptionExpired;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Push delivery failed.");
            return PushResult.Failed;
        }
    }
}
