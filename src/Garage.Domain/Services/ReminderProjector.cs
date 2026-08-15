using Garage.Domain.Entities;

namespace Garage.Domain.Services;

/// <summary>
/// Where a reminder stands right now. Distances and days are negative once past due,
/// so "how far overdue" and "how far to go" are the same number with opposite signs.
/// </summary>
public record ReminderProjection(
    Guid ReminderId,
    string Item,
    DueBand Band,
    DueTrigger LeadingTrigger,
    int? DueOdometer,
    DateOnly? DueDate,
    int? MilesRemaining,
    int? DaysRemaining,
    double? MileageProgress,
    double? TimeProgress,
    string TriggerDescription,
    string RemainingDescription,
    int? DaysUntilDue)
{
    public bool IsOverdue => Band == DueBand.Overdue;

    /// <summary>
    /// Orders items by how soon they arrive. Both triggers are already expressed in
    /// days, so a mileage item and a time item can be compared on one scale — sorting
    /// raw miles against raw days would put a 1,000-mile item behind a 100-day one.
    /// </summary>
    public int SortKey => DaysUntilDue ?? int.MaxValue;
}

/// <summary>
/// Stories S-2 and S-3. Turns a reminder's intervals into a band, a progress figure and
/// a sentence, using the vehicle's actual daily average rather than a fixed guess.
/// </summary>
public static class ReminderProjector
{
    /// <summary>
    /// How far ahead counts as "due soon". Six months of projected use, which is what
    /// puts a rotation 1,100 miles out and a filter four months out in the same band [1c].
    /// </summary>
    public const int DueSoonDays = 180;

    /// <summary>Fallback band threshold for a vehicle with no daily average yet.</summary>
    public const int DueSoonMilesWithoutRate = 1_000;

    public static ReminderProjection Project(
        Reminder reminder,
        int currentOdometer,
        DateOnly today,
        double? milesPerDay)
    {
        ArgumentNullException.ThrowIfNull(reminder);

        var dueOdometer = reminder.DueOdometer;
        var dueDate = reminder.DueDate;

        int? milesRemaining = dueOdometer is { } odo ? odo - currentOdometer : null;
        int? daysRemaining = dueDate is { } date ? date.DayNumber - today.DayNumber : null;

        // Both triggers are converted to days so they can be compared on one scale.
        var daysViaMileage = milesRemaining is { } miles
            ? DaysForMiles(miles, milesPerDay)
            : null;

        var leading = ChooseLeadingTrigger(daysViaMileage, daysRemaining, milesRemaining);
        var band = ChooseBand(daysViaMileage, daysRemaining, milesRemaining, milesPerDay);

        return new ReminderProjection(
            reminder.Id,
            reminder.Item,
            band,
            leading,
            dueOdometer,
            dueDate,
            milesRemaining,
            daysRemaining,
            Progress(currentOdometer - reminder.AnchorOdometer, reminder.MileageInterval),
            Progress(today.DayNumber - reminder.AnchorDate.DayNumber, MonthsInDays(reminder)),
            reminder.TriggerDescription,
            Describe(band, leading, milesRemaining, daysRemaining),
            Min(daysViaMileage, daysRemaining));
    }

    /// <summary>
    /// Days until a distance is covered. Negative distances stay negative so an overdue
    /// mileage trigger still sorts ahead of a future one.
    /// </summary>
    private static int? DaysForMiles(int miles, double? milesPerDay)
    {
        if (milesPerDay is not { } rate || rate <= 0)
        {
            return null;
        }

        return (int)Math.Ceiling(miles / rate);
    }

    private static DueTrigger ChooseLeadingTrigger(int? daysViaMileage, int? daysRemaining, int? milesRemaining)
    {
        if (daysViaMileage is null && daysRemaining is null)
        {
            // Mileage-only on a vehicle with no rate yet: still a mileage trigger.
            return milesRemaining is not null ? DueTrigger.Mileage : DueTrigger.None;
        }

        if (daysRemaining is null)
        {
            return DueTrigger.Mileage;
        }

        if (daysViaMileage is null)
        {
            return milesRemaining is not null && milesRemaining <= 0 ? DueTrigger.Mileage : DueTrigger.Time;
        }

        return daysViaMileage <= daysRemaining ? DueTrigger.Mileage : DueTrigger.Time;
    }

    /// <summary>Story S-2's three bands, decided by whichever trigger arrives first.</summary>
    private static DueBand ChooseBand(int? daysViaMileage, int? daysRemaining, int? milesRemaining, double? milesPerDay)
    {
        if (milesRemaining <= 0 || daysRemaining <= 0)
        {
            return DueBand.Overdue;
        }

        // Without a rate, a mileage trigger is judged on distance alone.
        if (milesPerDay is null or <= 0 && milesRemaining is { } miles)
        {
            if (miles <= DueSoonMilesWithoutRate)
            {
                return DueBand.DueSoon;
            }

            return daysRemaining <= DueSoonDays ? DueBand.DueSoon : DueBand.Later;
        }

        var soonest = Min(daysViaMileage, daysRemaining);
        return soonest is { } days && days <= DueSoonDays ? DueBand.DueSoon : DueBand.Later;
    }

    private static int? Min(int? a, int? b) => (a, b) switch
    {
        (null, null) => null,
        (not null, null) => a,
        (null, not null) => b,
        _ => Math.Min(a!.Value, b!.Value)
    };

    /// <summary>
    /// Fraction of the interval consumed. Can exceed 1 once past due, which the UI
    /// clamps when drawing the bar but keeps for the wording.
    /// </summary>
    private static double? Progress(int elapsed, int? interval) =>
        interval is > 0 ? Math.Max(0, (double)elapsed / interval.Value) : null;

    private static int? MonthsInDays(Reminder reminder) =>
        reminder.MonthInterval is { } months
            ? reminder.AnchorDate.AddMonths(months).DayNumber - reminder.AnchorDate.DayNumber
            : null;

    /// <summary>The line under each card: how far past due, or how far to go [1c].</summary>
    private static string Describe(DueBand band, DueTrigger leading, int? milesRemaining, int? daysRemaining)
    {
        if (band == DueBand.Overdue)
        {
            if (milesRemaining is { } miles and <= 0)
            {
                return $"{Math.Abs(miles):N0} mi past due";
            }

            var days = Math.Abs(daysRemaining ?? 0);
            return days == 0 ? "due today" : $"{DescribeDays(days)} past due";
        }

        return leading switch
        {
            DueTrigger.Mileage when milesRemaining is { } miles => $"{miles:N0} mi to go",
            DueTrigger.Time when daysRemaining is { } days => $"{DescribeDays(days)} to go",
            _ => "no trigger set"
        };
    }

    /// <summary>Months read better than days once past a few weeks.</summary>
    private static string DescribeDays(int days) => days switch
    {
        < 45 => $"{days} day{(days == 1 ? "" : "s")}",
        _ => $"{(int)Math.Round(days / 30.44)} mo"
    };
}
