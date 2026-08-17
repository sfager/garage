using Garage.Application.Abstractions;
using Garage.Application.Mileage;
using Garage.Domain;
using Garage.Domain.Common;
using Garage.Domain.Entities;
using Garage.Domain.Repositories;
using Garage.Domain.Services;

namespace Garage.Application.Maintenance;

/// <summary>
/// Epic E3. Owns the reminder lifecycle and the projections the maintenance screen
/// groups by. Projections always run against the vehicle's real daily average, so the
/// bands shift as the car's use changes (story S-3).
/// </summary>
public class MaintenanceService(
    IVehicleRepository vehicles,
    IReminderRepository reminders,
    IServiceRecordRepository serviceRecords,
    IMileageRepository mileage,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    /// <summary>Story S-2: every live reminder, projected and ready to group.</summary>
    public async Task<IReadOnlyList<ReminderCard>> ListUpcomingAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var vehicle = await RequireVehicleAsync(vehicleId, cancellationToken);
        var active = await reminders.ListActiveAsync(vehicleId, cancellationToken);
        var rate = await GetMilesPerDayAsync(vehicleId, cancellationToken);

        return [.. active
            .Select(r => ToCard(r, vehicle.CurrentOdometer, rate))
            .OrderBy(c => c.Band)
            .ThenBy(SortKey)];
    }

    /// <summary>Story S-5's list of active reminders with their triggers [1k].</summary>
    public async Task<IReadOnlyList<ReminderCard>> ListAllAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var vehicle = await RequireVehicleAsync(vehicleId, cancellationToken);
        var all = await reminders.ListAllAsync(vehicleId, cancellationToken);
        var rate = await GetMilesPerDayAsync(vehicleId, cancellationToken);

        return [.. all
            .Select(r => ToCard(r, vehicle.CurrentOdometer, rate))
            .OrderBy(c => c.IsDismissed)
            .ThenBy(c => c.Band)
            .ThenBy(SortKey)];
    }

    /// <summary>
    /// Story S-1: what the reminder would come due at, in words, before it is saved.
    /// Uses the vehicle's current odometer and today as the anchor, which is what a
    /// newly created reminder measures from.
    /// </summary>
    public async Task<ReminderPreview> PreviewAsync(
        Guid vehicleId,
        ReminderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var vehicle = await RequireVehicleAsync(vehicleId, cancellationToken);

        if (request.HasNoTrigger)
        {
            return new ReminderPreview(null, null, "Set a mileage interval, a month interval, or both.");
        }

        var (anchorOdometer, anchorDate) = await GetAnchorAsync(request.Id, vehicle, cancellationToken);

        var dueOdometer = request.MileageInterval is { } miles ? anchorOdometer + miles : (int?)null;
        var dueDate = request.MonthInterval is { } months ? anchorDate.AddMonths(months) : (DateOnly?)null;

        var explanation = (dueOdometer, dueDate) switch
        {
            (not null, not null) => $"Whichever comes first — {dueOdometer:N0} mi or {dueDate:MMM yyyy}",
            (not null, null) => $"Due at {dueOdometer:N0} mi",
            (null, not null) => $"Due {dueDate:MMM yyyy}",
            _ => "Set a mileage interval, a month interval, or both."
        };

        return new ReminderPreview(dueOdometer, dueDate, explanation);
    }

    public async Task<Reminder> SaveAsync(
        Guid vehicleId,
        ReminderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var vehicle = await RequireVehicleAsync(vehicleId, cancellationToken);

        if (request.Id is { } existingId)
        {
            var existing = await RequireReminderAsync(existingId, cancellationToken);
            existing.Rename(request.Item);
            existing.UpdateIntervals(request.MileageInterval, request.MonthInterval);
            existing.SetRepeatAfterService(request.RepeatAfterService);
            existing.SetNotifications(request.NotificationsEnabled);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return existing;
        }

        // A new reminder measures from where the vehicle stands today.
        var reminder = new Reminder(
            vehicle.Id,
            request.Item,
            request.MileageInterval,
            request.MonthInterval,
            vehicle.CurrentOdometer,
            clock.Today,
            request.RepeatAfterService);

        reminder.SetNotifications(request.NotificationsEnabled);

        vehicle.AddReminder(reminder);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return reminder;
    }

    /// <summary>Story S-4: defer without changing the interval.</summary>
    public async Task SnoozeAsync(Guid reminderId, SnoozeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reminder = await RequireReminderAsync(reminderId, cancellationToken);
        var vehicle = await RequireVehicleAsync(reminder.VehicleId, cancellationToken);

        reminder.Snooze(vehicle.CurrentOdometer, clock.Today, request.ByMiles, request.ByMonths);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DismissAsync(Guid reminderId, CancellationToken cancellationToken = default)
    {
        var reminder = await RequireReminderAsync(reminderId, cancellationToken);
        reminder.Dismiss();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ReinstateAsync(Guid reminderId, CancellationToken cancellationToken = default)
    {
        var reminder = await RequireReminderAsync(reminderId, cancellationToken);
        reminder.Reinstate();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Story S-5's per-reminder switch.</summary>
    public async Task SetNotificationsAsync(Guid reminderId, bool enabled, CancellationToken cancellationToken = default)
    {
        var reminder = await RequireReminderAsync(reminderId, cancellationToken);
        reminder.SetNotifications(enabled);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid reminderId, CancellationToken cancellationToken = default)
    {
        var reminder = await RequireReminderAsync(reminderId, cancellationToken);
        reminders.Remove(reminder);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Story S-4: completing a repeating item re-anchors it to the service just logged,
    /// so the next due point falls out of the interval. E4's wizard calls this.
    /// </summary>
    public async Task CompleteAsync(Guid reminderId, int odometer, DateOnly date, CancellationToken cancellationToken = default)
    {
        var reminder = await RequireReminderAsync(reminderId, cancellationToken);
        reminder.CompleteAt(odometer, date);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Story S-6: service history, newest first.</summary>
    public async Task<IReadOnlyList<ServiceHistoryEntry>> ListHistoryAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        await RequireVehicleAsync(vehicleId, cancellationToken);
        var records = await serviceRecords.ListForVehicleAsync(vehicleId, cancellationToken);

        return [.. records.Select(r => new ServiceHistoryEntry(
            r.Id,
            r.Date,
            r.Odometer,
            r.Summary,
            r.Category,
            r.TotalCost,
            r.Shop,
            r.Receipts.Count))];
    }

    public async Task<ServiceRecord?> GetServiceRecordAsync(Guid recordId, CancellationToken cancellationToken = default)
    {
        var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);
        return await serviceRecords.GetForHouseholdAsync(recordId, householdId, cancellationToken);
    }

    /// <summary>Story L-2: the shop field remembers where work has been done before.</summary>
    public async Task<IReadOnlyList<string>> ListShopsAsync(CancellationToken cancellationToken = default)
    {
        var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);
        return await serviceRecords.ListShopsAsync(householdId, cancellationToken);
    }

    /// <summary>
    /// The vehicle's rate, which every projection depends on (story S-3). Private
    /// deliberately: it does not scope to the household, and both callers have already
    /// done so.
    /// </summary>
    private async Task<double?> GetMilesPerDayAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var points = await mileage.ListPointsAsync(vehicleId, cancellationToken);
        var summary = MileageCalculator.Summarize(
            points.Select(p => new MileagePoint(p.Date, p.Odometer, p.IsReading)),
            clock.Today);

        return summary.MilesPerDay;
    }

    private ReminderCard ToCard(Reminder reminder, int currentOdometer, double? milesPerDay) => new(
        reminder.Id,
        reminder.Item,
        reminder.MileageInterval,
        reminder.MonthInterval,
        reminder.IntervalDescription,
        reminder.RepeatAfterService,
        reminder.NotificationsEnabled,
        reminder.IsDismissed,
        ReminderProjector.Project(reminder, currentOdometer, clock.Today, milesPerDay));

    /// <summary>Within a band, the most pressing item comes first.</summary>
    private static int SortKey(ReminderCard card) => card.Projection.SortKey;

    /// <summary>
    /// An edit keeps the reminder's existing anchor; a new one starts from today,
    /// so the preview matches what saving would actually produce.
    /// </summary>
    private async Task<(int Odometer, DateOnly Date)> GetAnchorAsync(
        Guid? reminderId,
        Vehicle vehicle,
        CancellationToken cancellationToken)
    {
        if (reminderId is not { } id)
        {
            return (vehicle.CurrentOdometer, clock.Today);
        }

        var existing = await RequireReminderAsync(id, cancellationToken);
        return (existing.AnchorOdometer, existing.AnchorDate);
    }

    private async Task<Vehicle> RequireVehicleAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);
        return await vehicles.GetForHouseholdAsync(vehicleId, householdId, cancellationToken)
            ?? throw new DomainException("That vehicle is not in your garage.");
    }

    private async Task<Reminder> RequireReminderAsync(Guid reminderId, CancellationToken cancellationToken)
    {
        var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);
        return await reminders.GetForHouseholdAsync(reminderId, householdId, cancellationToken)
            ?? throw new DomainException("That reminder is not in your garage.");
    }
}
