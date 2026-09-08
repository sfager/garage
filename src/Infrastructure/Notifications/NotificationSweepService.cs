using Garage.Application.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Garage.Infrastructure.Notifications;

public class NotificationSweepOptions
{
    /// <summary>How often to look for newly due items. Hourly is well inside a day's notice.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Lets the host settle before the first sweep touches the database.</summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromSeconds(30);

    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Story S-5's timer. Runs the sweep on a schedule so a due point reaches the user
/// without them opening the app.
/// </summary>
public class NotificationSweepService(
    IServiceScopeFactory scopeFactory,
    NotificationSweepOptions options,
    ILogger<NotificationSweepService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("Notification sweep is disabled.");
            return;
        }

        try
        {
            await Task.Delay(options.StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(options.Interval);

        do
        {
            try
            {
                // A scope per sweep: the DbContext is scoped, and this runs outside a request.
                await using var scope = scopeFactory.CreateAsyncScope();
                var notifications = scope.ServiceProvider.GetRequiredService<NotificationService>();

                var result = await notifications.SweepAsync(stoppingToken);

                if (result.Sent > 0 || result.SubscriptionsDropped > 0)
                {
                    logger.LogInformation(
                        "Notification sweep: {Sent} sent, {Skipped} already told, {Dropped} dead subscriptions across {Households} households.",
                        result.Sent, result.Skipped, result.SubscriptionsDropped, result.HouseholdsScanned);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // A sweep that throws must not take the timer down with it.
                logger.LogError(ex, "The notification sweep failed; it will run again next interval.");
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
