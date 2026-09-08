namespace Garage.Domain.Services;

/// <summary>
/// A dated odometer value from anywhere in the vehicle's history. Readings are marked
/// because "miles since last" measures between readings [1j], while the daily average
/// uses every point as evidence of movement.
/// </summary>
public readonly record struct MileagePoint(DateOnly Date, int Odometer, bool IsReading = false);

/// <summary>
/// Story M-3. The daily average feeds the "due in X miles" projections in maintenance,
/// so an average built from too little history would quietly distort every reminder.
/// When that is the case the summary says why instead of returning zero.
/// </summary>
public record MileageSummary(
    int CurrentOdometer,
    int? MilesSinceLast,
    DateOnly? SinceDate,
    double? MilesPerDay,
    string? UnavailableReason,
    int DaysCovered)
{
    public bool HasAverage => MilesPerDay is not null;
}

public static class MileageCalculator
{
    /// <summary>
    /// How far back the average looks. Long enough to survive a quiet fortnight,
    /// short enough that a car's recent use outweighs how it was driven two years ago.
    /// </summary>
    public const int DefaultWindowDays = 90;

    /// <summary>
    /// Summarises a vehicle's mileage from its recorded points. Points may arrive in
    /// any order and may contain duplicates for a single day.
    /// </summary>
    public static MileageSummary Summarize(
        IEnumerable<MileagePoint> points,
        DateOnly today,
        int windowDays = DefaultWindowDays)
    {
        ArgumentNullException.ThrowIfNull(points);

        var ordered = points
            .OrderBy(p => p.Date)
            .ThenBy(p => p.Odometer)
            .ToList();

        if (ordered.Count == 0)
        {
            return new MileageSummary(0, null, null, null, "No mileage recorded yet.", 0);
        }

        var latest = ordered[^1];

        // Measured against the last reading rather than the last event of any kind:
        // trips and fill-ups in between are the miles being counted, not the mark to
        // count from [1j].
        int? milesSinceLast = null;
        DateOnly? sinceDate = null;

        var readings = ordered.Where(p => p.IsReading).ToList();
        var previousReading = readings.Count switch
        {
            0 => (MileagePoint?)null,
            _ when readings[^1] == latest => readings.Count >= 2 ? readings[^2] : null,
            _ => readings[^1]
        };

        if (previousReading is { } mark)
        {
            milesSinceLast = latest.Odometer - mark.Odometer;
            sinceDate = mark.Date;
        }

        var (milesPerDay, reason, daysCovered) = AverageOver(ordered, today, windowDays);

        return new MileageSummary(latest.Odometer, milesSinceLast, sinceDate, milesPerDay, reason, daysCovered);
    }

    /// <summary>
    /// Averages over the window, widening to the whole history when the window holds
    /// too little to say anything. Two points on the same day span no time and cannot
    /// produce a rate.
    /// </summary>
    private static (double? MilesPerDay, string? Reason, int DaysCovered) AverageOver(
        List<MileagePoint> ordered,
        DateOnly today,
        int windowDays)
    {
        var cutoff = today.AddDays(-windowDays);
        var windowed = ordered.Where(p => p.Date >= cutoff).ToList();

        var sample = Spans(windowed) ? windowed : ordered;

        if (!Spans(sample))
        {
            return (null, sample.Count < 2
                ? "Add another reading to see a daily average."
                : "All readings are from the same day, so there is no rate yet.", 0);
        }

        var first = sample[0];
        var last = sample[^1];
        var days = last.Date.DayNumber - first.Date.DayNumber;
        var miles = last.Odometer - first.Odometer;

        return ((double)miles / days, null, days);
    }

    private static bool Spans(List<MileagePoint> sample) =>
        sample.Count >= 2 && sample[^1].Date.DayNumber > sample[0].Date.DayNumber;

    /// <summary>
    /// Projects how many days until the vehicle covers a distance at its current rate.
    /// Used by maintenance to turn "1,100 miles to go" into a date (story S-3).
    /// </summary>
    public static int? DaysToCover(int miles, double? milesPerDay)
    {
        if (milesPerDay is not { } rate || rate <= 0 || miles < 0)
        {
            return null;
        }

        return (int)Math.Ceiling(miles / rate);
    }
}
