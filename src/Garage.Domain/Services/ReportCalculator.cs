using Garage.Domain.ValueObjects;

namespace Garage.Domain.Services;

/// <summary>Story R-1's four summary figures [1m].</summary>
public record CostDashboard(
    decimal TotalSpend,
    decimal? CostPerMile,
    int MilesDriven,
    string? LargestLineItem,
    decimal? LargestLineAmount,
    DateOnly? LargestLineDate,
    IReadOnlyList<MonthlySpend> ByMonth,
    IReadOnlyList<CategoryTotal> ByCategory);

/// <summary>A month of the spend chart, split service against fuel [1m].</summary>
public record MonthlySpend(DateOnly Month, decimal Service, decimal Fuel)
{
    public decimal Total => Service + Fuel;
}

/// <summary>A row of the "where it went" breakdown [1m].</summary>
public record CategoryTotal(string Category, decimal Amount)
{
    /// <summary>Share of the largest category, which is what the bars are drawn against.</summary>
    public double ShareOfLargest { get; init; }
}

/// <summary>Story R-3: one vehicle's column in the side-by-side comparison.</summary>
public record VehicleComparison(
    Guid VehicleId,
    string Nickname,
    decimal TotalSpend,
    int MilesDriven,
    decimal? CostPerMile,
    double? AverageMpg,
    decimal AnnualisedSpend);

public static class ReportCalculator
{
    /// <summary>
    /// Story R-1. Every figure is derived from the same filtered set of cost lines, so
    /// the vehicle and range filters cannot leave one number covering a different period
    /// from its neighbour.
    /// </summary>
    public static CostDashboard BuildDashboard(
        IReadOnlyList<CostLine> lines,
        IReadOnlyList<OdometerPoint> points)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(points);

        var total = lines.Sum(l => l.Cost);
        var miles = MilesDriven(points);
        var largest = lines.OrderByDescending(l => l.Cost).FirstOrDefault();

        var byMonth = lines
            .GroupBy(l => new DateOnly(l.Date.Year, l.Date.Month, 1))
            .OrderBy(g => g.Key)
            .Select(g => new MonthlySpend(
                g.Key,
                g.Where(l => l.Kind == CostKind.Service).Sum(l => l.Cost),
                g.Where(l => l.Kind == CostKind.Fuel).Sum(l => l.Cost)))
            .ToList();

        var categoryTotals = lines
            .GroupBy(CategoryNameOf)
            .Select(g => new CategoryTotal(g.Key, g.Sum(l => l.Cost)))
            .OrderByDescending(c => c.Amount)
            .ToList();

        var largestCategory = categoryTotals.Count > 0 ? categoryTotals[0].Amount : 0m;
        var byCategory = categoryTotals
            .Select(c => c with
            {
                ShareOfLargest = largestCategory > 0 ? (double)(c.Amount / largestCategory) : 0
            })
            .ToList();

        return new CostDashboard(
            total,
            miles > 0 ? total / miles : null,
            miles,
            largest?.Item,
            largest?.Cost,
            largest?.Date,
            byMonth,
            byCategory);
    }

    /// <summary>
    /// Story R-3. Annualised spend scales the range to a year so a six-month window and
    /// a twelve-month one can still be set beside each other.
    /// </summary>
    public static IReadOnlyList<VehicleComparison> Compare(
        IReadOnlyList<CostLine> lines,
        IReadOnlyList<OdometerPoint> points,
        IReadOnlyDictionary<Guid, double?> mpgByVehicle,
        int daysInRange)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(mpgByVehicle);

        var vehicleIds = lines.Select(l => l.VehicleId)
            .Union(points.Select(p => p.VehicleId))
            .Distinct();

        var comparisons = new List<VehicleComparison>();

        foreach (var vehicleId in vehicleIds)
        {
            var vehicleLines = lines.Where(l => l.VehicleId == vehicleId).ToList();
            var vehiclePoints = points.Where(p => p.VehicleId == vehicleId).ToList();

            var spend = vehicleLines.Sum(l => l.Cost);
            var miles = MilesDriven(vehiclePoints);
            var nickname = vehicleLines.FirstOrDefault()?.VehicleNickname ?? "Vehicle";

            var annualised = daysInRange > 0
                ? spend * 365m / daysInRange
                : spend;

            comparisons.Add(new VehicleComparison(
                vehicleId,
                nickname,
                spend,
                miles,
                miles > 0 ? spend / miles : null,
                mpgByVehicle.GetValueOrDefault(vehicleId),
                annualised));
        }

        return [.. comparisons.OrderBy(c => c.Nickname)];
    }

    /// <summary>
    /// Distance covered inside the window, measured per vehicle and summed. Taking the
    /// spread across the whole household at once would count the gap between two cars'
    /// odometers as miles driven.
    /// </summary>
    public static int MilesDriven(IReadOnlyList<OdometerPoint> points) =>
        points
            .GroupBy(p => p.VehicleId)
            .Sum(g => g.Count() < 2 ? 0 : g.Max(p => p.Odometer) - g.Min(p => p.Odometer));

    /// <summary>
    /// Fuel is its own category in the breakdown; service spend is grouped by the
    /// category the record carries [1m].
    /// </summary>
    private static string CategoryNameOf(CostLine line) => line.Kind switch
    {
        CostKind.Fuel => "Fuel",
        _ => line.Category switch
        {
            ServiceCategory.ScheduledService => "Scheduled service",
            ServiceCategory.Repair => "Repairs",
            ServiceCategory.Tires => "Tires",
            ServiceCategory.Inspection => "Inspection",
            _ => "Other"
        }
    };
}
