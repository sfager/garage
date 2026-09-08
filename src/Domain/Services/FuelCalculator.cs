namespace Garage.Domain.Services;

/// <summary>A fill-up reduced to what efficiency needs from it.</summary>
public readonly record struct FuelFill(
    Guid Id,
    DateOnly Date,
    int Odometer,
    decimal Gallons,
    decimal TotalCost,
    bool IsPartialFill);

/// <summary>
/// One fill-up with the efficiency it produced. <see cref="Mpg"/> is null when the fill
/// cannot close a tank interval — the first full fill, or any partial one.
/// </summary>
public record FuelFillEfficiency(
    Guid Id,
    DateOnly Date,
    int Odometer,
    decimal Gallons,
    decimal TotalCost,
    bool IsPartialFill,
    double? Mpg,
    int? MilesCovered);

/// <summary>
/// Story G-2. Insufficient data is reported as a reason rather than a zero, because a
/// zero here reads as "this car does nought to the gallon" instead of "we cannot tell yet".
/// </summary>
public record FuelEfficiency(
    double? AverageMpg,
    string? UnavailableReason,
    IReadOnlyList<FuelFillEfficiency> Fills,
    int MilesMeasured,
    decimal GallonsMeasured);

public static class FuelCalculator
{
    /// <summary>
    /// Tank-to-tank efficiency. A full fill returns the tank to the same level, so the
    /// fuel burned over the distance since the last full fill is everything added in
    /// between — partial fills included, which is why they are counted but never
    /// attributed an MPG of their own (story G-2).
    /// </summary>
    public static FuelEfficiency Calculate(IEnumerable<FuelFill> fills)
    {
        ArgumentNullException.ThrowIfNull(fills);

        var ordered = fills
            .OrderBy(f => f.Odometer)
            .ThenBy(f => f.Date)
            .ToList();

        var results = new List<FuelFillEfficiency>(ordered.Count);

        int? lastFullOdometer = null;
        var gallonsSinceLastFull = 0m;
        var totalMiles = 0;
        var totalGallons = 0m;

        foreach (var fill in ordered)
        {
            gallonsSinceLastFull += fill.Gallons;

            double? mpg = null;
            int? miles = null;

            if (!fill.IsPartialFill)
            {
                if (lastFullOdometer is { } previous)
                {
                    var distance = fill.Odometer - previous;

                    if (distance > 0 && gallonsSinceLastFull > 0)
                    {
                        miles = distance;
                        mpg = distance / (double)gallonsSinceLastFull;

                        totalMiles += distance;
                        totalGallons += gallonsSinceLastFull;
                    }
                }

                // Either way this fill becomes the new baseline and the tank starts over.
                lastFullOdometer = fill.Odometer;
                gallonsSinceLastFull = 0m;
            }

            results.Add(new FuelFillEfficiency(
                fill.Id, fill.Date, fill.Odometer, fill.Gallons, fill.TotalCost, fill.IsPartialFill, mpg, miles));
        }

        // Newest first for display.
        results.Reverse();

        var average = totalGallons > 0 ? totalMiles / (double)totalGallons : (double?)null;

        return new FuelEfficiency(
            average,
            average is null ? ExplainMissingAverage(ordered) : null,
            results,
            totalMiles,
            totalGallons);
    }

    private static string ExplainMissingAverage(List<FuelFill> ordered)
    {
        if (ordered.Count == 0)
        {
            return "No fill-ups logged yet.";
        }

        var fullFills = ordered.Count(f => !f.IsPartialFill);

        return fullFills switch
        {
            0 => "Every fill-up so far is a partial, so there is no full tank to measure between.",
            1 => "Log a second full fill-up to see miles per gallon.",
            _ => "The fill-ups logged do not cover any distance yet."
        };
    }
}
