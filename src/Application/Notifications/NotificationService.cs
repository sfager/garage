using Garage.Application.Abstractions;
using Garage.Domain;
using Garage.Domain.Entities;
using Garage.Domain.Repositories;
using Garage.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Garage.Application.Notifications;

/// <summary>What one sweep did, for the log and for tests.</summary>
public record NotificationSweepResult(int HouseholdsScanned, int Sent, int Skipped, int SubscriptionsDropped);

/// <summary>
/// Story S-5. Finds what has fallen due and tells the household's browsers about it.
///
/// A due point stays due until the work is done, so the same reminder would otherwise be
/// announced on every sweep. Each notification is recorded against the due point it was
/// about, which keeps a standing item quiet while still speaking up when the due point
/// moves — after a service, or a snooze.
/// </summary>
public class NotificationService(
    INotificationScanRepository scan,
    IPushSubscriptionRepository subscriptions,
    ISentNotificationRepository sent,
    IMileageRepository mileage,
    IPushSender sender,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<NotificationService> logger)
{
    /// <summary>Notes older than this are pruned; nothing stays due that long unnoticed.</summary>
    private static readonly TimeSpan NoteRetention = TimeSpan.FromDays(400);

    public async Task<NotificationSweepResult> SweepAsync(CancellationToken cancellationToken = default)
    {
        if (!sender.IsConfigured)
        {
            logger.LogDebug("Push is not configured; skipping the notification sweep.");
            return new NotificationSweepResult(0, 0, 0, 0);
        }

        var households = await scan.ListHouseholdIdsWithSubscriptionsAsync(cancellationToken);
        int totalSent = 0, totalSkipped = 0, totalDropped = 0;

        foreach (var householdId in households)
        {
            var (s, skipped, dropped) = await SweepHouseholdAsync(householdId, cancellationToken);
            totalSent += s;
            totalSkipped += skipped;
            totalDropped += dropped;
        }

        await sent.PruneAsync(clock.UtcNow - NoteRetention, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new NotificationSweepResult(households.Count, totalSent, totalSkipped, totalDropped);
    }

    private async Task<(int Sent, int Skipped, int Dropped)> SweepHouseholdAsync(
        Guid householdId,
        CancellationToken cancellationToken)
    {
        var due = await FindDueAsync(householdId, cancellationToken);
        if (due.Count == 0)
        {
            return (0, 0, 0);
        }

        var alreadySent = await sent.ListSentKeysAsync(householdId, cancellationToken);
        var pending = due.Where(d => !alreadySent.Contains(d.SubjectKey)).ToList();
        var skipped = due.Count - pending.Count;

        if (pending.Count == 0)
        {
            return (0, skipped, 0);
        }

        var browsers = await subscriptions.ListForHouseholdAsync(householdId, cancellationToken);
        if (browsers.Count == 0)
        {
            return (0, skipped, 0);
        }

        int delivered = 0, dropped = 0;

        foreach (var item in pending)
        {
            var reachedSomeone = false;

            foreach (var browser in browsers)
            {
                var result = await sender.SendAsync(browser, item.Message, cancellationToken);

                switch (result)
                {
                    case PushResult.Sent:
                        browser.MarkUsed(clock.UtcNow);
                        reachedSomeone = true;
                        delivered++;
                        break;

                    case PushResult.SubscriptionExpired:
                        // The browser has been reinstalled or the permission revoked.
                        subscriptions.Remove(browser);
                        dropped++;
                        break;

                    case PushResult.Failed:
                        logger.LogWarning("Push failed for one subscription; will retry next sweep.");
                        break;
                }
            }

            // Only record it as told if it actually reached a browser, so a transient
            // outage does not silently swallow the notification for good.
            if (reachedSomeone)
            {
                await sent.AddAsync(new SentNotification(householdId, item.SubjectKey, item.Message.Title), cancellationToken);
            }
        }

        return (delivered, skipped, dropped);
    }

    /// <summary>
    /// Everything worth mentioning right now: reminders whose trigger has arrived, and
    /// documents inside the 30-day expiry window [1k, 1l].
    /// </summary>
    private async Task<List<DueNotification>> FindDueAsync(Guid householdId, CancellationToken cancellationToken)
    {
        var today = clock.Today;
        var due = new List<DueNotification>();

        foreach (var (reminder, nickname, odometer) in await scan.ListActiveRemindersAsync(householdId, cancellationToken))
        {
            // Story S-5: the per-reminder switch decides whether this one may speak.
            if (!reminder.NotificationsEnabled)
            {
                continue;
            }

            var rate = await MilesPerDayAsync(reminder.VehicleId, cancellationToken);
            var projection = ReminderProjector.Project(reminder, odometer, today, rate);

            if (projection.Band != DueBand.Overdue)
            {
                continue;
            }

            due.Add(new DueNotification(
                $"reminder:{reminder.Id}:{reminder.DueOdometer}:{reminder.DueDate:yyyy-MM-dd}",
                new PushMessage(
                    $"{nickname}: {reminder.Item}",
                    $"{projection.RemainingDescription} — {projection.TriggerDescription}",
                    "/service",
                    $"reminder-{reminder.Id}")));
        }

        var through = today.AddDays(30);

        foreach (var (document, nickname) in await scan.ListExpiringDocumentsAsync(householdId, through, cancellationToken))
        {
            var days = document.DaysUntilExpiry(today);

            due.Add(new DueNotification(
                $"document:{document.Id}:{document.ExpiresOn:yyyy-MM-dd}",
                new PushMessage(
                    $"{nickname}: {document.Title}",
                    days < 0 ? $"Expired {Math.Abs(days.Value)} days ago" : $"Expires in {days} days",
                    "/documents",
                    $"document-{document.Id}")));
        }

        return due;
    }

    private async Task<double?> MilesPerDayAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        var points = await mileage.ListPointsAsync(vehicleId, cancellationToken);
        return MileageCalculator
            .Summarize(points.Select(p => new MileagePoint(p.Date, p.Odometer, p.IsReading)), clock.Today)
            .MilesPerDay;
    }

    private record DueNotification(string SubjectKey, PushMessage Message);
}
