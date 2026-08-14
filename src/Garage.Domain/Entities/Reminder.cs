using Garage.Domain.Common;

namespace Garage.Domain.Entities;

/// <summary>
/// A scheduled service item. It can fire on mileage, on elapsed months, or on
/// whichever of the two arrives first (story S-1).
///
/// The anchor is the point the intervals are measured from: where the vehicle stood
/// when the reminder was created, and thereafter where it stood at the last service.
/// </summary>
public class Reminder : Entity
{
    private Reminder() { }

    public Reminder(
        Guid vehicleId,
        string item,
        int? mileageInterval,
        int? monthInterval,
        int anchorOdometer,
        DateOnly anchorDate,
        bool repeatAfterService = true)
    {
        if (string.IsNullOrWhiteSpace(item))
        {
            throw new DomainException("A reminder needs to say what it is for.");
        }

        if (mileageInterval is null && monthInterval is null)
        {
            throw new DomainException("Set a mileage interval, a month interval, or both.");
        }

        if (mileageInterval is <= 0)
        {
            throw new DomainException("The mileage interval has to be greater than zero.");
        }

        if (monthInterval is <= 0)
        {
            throw new DomainException("The month interval has to be greater than zero.");
        }

        VehicleId = vehicleId;
        Item = item.Trim();
        MileageInterval = mileageInterval;
        MonthInterval = monthInterval;
        AnchorOdometer = anchorOdometer;
        AnchorDate = anchorDate;
        RepeatAfterService = repeatAfterService;
    }

    public Guid VehicleId { get; private set; }
    public Vehicle? Vehicle { get; private set; }

    public string Item { get; private set; } = string.Empty;
    public int? MileageInterval { get; private set; }
    public int? MonthInterval { get; private set; }
    public bool RepeatAfterService { get; private set; }

    /// <summary>Story S-5: a per-reminder notification switch, independent of the reminder itself.</summary>
    public bool NotificationsEnabled { get; private set; } = true;

    public int AnchorOdometer { get; private set; }
    public DateOnly AnchorDate { get; private set; }

    /// <summary>Set by a snooze; pushes the effective due point out without changing the interval.</summary>
    public int? SnoozedToOdometer { get; private set; }
    public DateOnly? SnoozedToDate { get; private set; }

    public bool IsDismissed { get; private set; }
    public DateTimeOffset CreatedUtc { get; private set; } = DateTimeOffset.UtcNow;

    /// <summary>The odometer this is due at, snooze included. Null when it is time-only.</summary>
    public int? DueOdometer => MileageInterval is null
        ? null
        : Math.Max(AnchorOdometer + MileageInterval.Value, SnoozedToOdometer ?? int.MinValue);

    /// <summary>The date this is due on, snooze included. Null when it is mileage-only.</summary>
    public DateOnly? DueDate
    {
        get
        {
            if (MonthInterval is null)
            {
                return SnoozedToDate;
            }

            var due = AnchorDate.AddMonths(MonthInterval.Value);
            return SnoozedToDate > due ? SnoozedToDate : due;
        }
    }

    /// <summary>The plain-English trigger line the wireframes print under each item.</summary>
    public string TriggerDescription => (MileageInterval, MonthInterval) switch
    {
        (not null, not null) => $"{DueOdometer:N0} mi or {DueDate:MMM yyyy} — whichever first",
        (not null, null) => $"{DueOdometer:N0} mi",
        (null, not null) => $"{DueDate:MMM yyyy}",
        _ => "no trigger set"
    };

    public string IntervalDescription => (MileageInterval, MonthInterval) switch
    {
        (not null, not null) => $"{MileageInterval:N0} mi / {MonthInterval} mo",
        (not null, null) => $"{MileageInterval:N0} mi",
        (null, not null) => $"{MonthInterval} mo",
        _ => "—"
    };

    public void UpdateIntervals(int? mileageInterval, int? monthInterval)
    {
        if (mileageInterval is null && monthInterval is null)
        {
            throw new DomainException("Set a mileage interval, a month interval, or both.");
        }

        if (mileageInterval is <= 0 || monthInterval is <= 0)
        {
            throw new DomainException("Intervals have to be greater than zero.");
        }

        MileageInterval = mileageInterval;
        MonthInterval = monthInterval;
    }

    public void Rename(string item)
    {
        if (string.IsNullOrWhiteSpace(item))
        {
            throw new DomainException("A reminder needs to say what it is for.");
        }

        Item = item.Trim();
    }

    public void SetRepeatAfterService(bool repeat) => RepeatAfterService = repeat;

    public void SetNotifications(bool enabled) => NotificationsEnabled = enabled;

    /// <summary>
    /// Story S-4: completing a repeating item re-anchors it to the service that was
    /// just logged, so the next due point falls out of the interval automatically.
    /// A one-shot reminder is retired instead.
    /// </summary>
    public void CompleteAt(int odometer, DateOnly date)
    {
        SnoozedToOdometer = null;
        SnoozedToDate = null;

        if (!RepeatAfterService)
        {
            IsDismissed = true;
            return;
        }

        AnchorOdometer = odometer;
        AnchorDate = date;
    }

    /// <summary>Defers the due point by a distance, a period, or both.</summary>
    public void Snooze(int currentOdometer, DateOnly today, int? byMiles, int? byMonths)
    {
        if (byMiles is null && byMonths is null)
        {
            throw new DomainException("Choose how far to snooze — a distance or a period.");
        }

        if (byMiles is not null)
        {
            SnoozedToOdometer = Math.Max(currentOdometer, DueOdometer ?? currentOdometer) + byMiles.Value;
        }

        if (byMonths is not null)
        {
            var from = DueDate is { } due && due > today ? due : today;
            SnoozedToDate = from.AddMonths(byMonths.Value);
        }
    }

    public void Dismiss() => IsDismissed = true;

    public void Reinstate() => IsDismissed = false;
}
