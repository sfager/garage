using System.Globalization;
using System.Text;
using Garage.Application.Abstractions;
using Garage.Domain.Repositories;
using Garage.Domain.Services;

namespace Garage.Application.Reporting;

/// <summary>
/// Epic E7. One load builds every panel of the reports screen from a single filtered set
/// of cost lines, which is what makes story R-1's "apply to every figure on screen" true
/// by construction rather than by remembering to pass the filter around.
/// </summary>
public class ReportService(
    IReportRepository reports,
    IFuelRepository fuel,
    IVehicleRepository vehicles,
    ICurrentUser currentUser,
    IClock clock)
{
    public async Task<ReportScreen> GetScreenAsync(ReportFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);
        var (from, to) = ResolveRange(filter.Range);

        var allLines = await reports.ListCostLinesAsync(householdId, from, to, cancellationToken);
        var allPoints = await reports.ListOdometerPointsAsync(householdId, from, to, cancellationToken);

        // The vehicle filter applies to everything; the category filter is the table's alone.
        var lines = Filter(allLines, filter.VehicleId);
        var points = filter.VehicleId is { } vehicleId
            ? [.. allPoints.Where(p => p.VehicleId == vehicleId)]
            : allPoints;

        var dashboard = ReportCalculator.BuildDashboard(lines, points);

        var history = Sort(
            filter.Category is { } category
                ? [.. lines.Where(l => CategoryLabel(l) == category)]
                : lines,
            filter.Sort,
            filter.Descending);

        var mpg = await BuildMpgAsync(allLines, cancellationToken);
        var comparison = ReportCalculator.Compare(allLines, allPoints, mpg, to.DayNumber - from.DayNumber);

        var categories = lines.Select(CategoryLabel).Distinct().OrderBy(c => c).ToList();

        return new ReportScreen(dashboard, history, comparison, categories, from, to, Describe(filter.Range, from, to));
    }

    /// <summary>Story R-4: the export honours the filters that produced the table.</summary>
    public async Task<CsvExport> ExportAsync(ReportFilter filter, CancellationToken cancellationToken = default)
    {
        var screen = await GetScreenAsync(filter, cancellationToken);

        var builder = new StringBuilder();
        builder.AppendLine("Date,Vehicle,Odometer,Category,Item,Shop,Cost");

        foreach (var line in screen.History)
        {
            builder.AppendLine(string.Join(',', new[]
            {
                line.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Escape(line.VehicleNickname),
                line.Odometer.ToString(CultureInfo.InvariantCulture),
                Escape(CategoryLabel(line)),
                Escape(line.Item),
                Escape(line.Shop ?? string.Empty),
                line.Cost.ToString("F2", CultureInfo.InvariantCulture)
            }));
        }

        var fileName = $"garage-{screen.From:yyyyMMdd}-{screen.To:yyyyMMdd}.csv";
        return new CsvExport(fileName, builder.ToString(), screen.History.Count);
    }

    /// <summary>
    /// Average MPG per vehicle, which only the fuel log can answer — the cost lines know
    /// what fuel cost but not how far it went.
    /// </summary>
    private async Task<Dictionary<Guid, double?>> BuildMpgAsync(
        IReadOnlyList<CostLine> lines,
        CancellationToken cancellationToken)
    {
        var mpg = new Dictionary<Guid, double?>();

        foreach (var vehicleId in lines.Select(l => l.VehicleId).Distinct())
        {
            var entries = await fuel.ListForVehicleAsync(vehicleId, cancellationToken);
            var efficiency = FuelCalculator.Calculate(entries.Select(e =>
                new FuelFill(e.Id, e.Date, e.Odometer, e.Gallons, e.TotalCost, e.IsPartialFill)));

            mpg[vehicleId] = efficiency.AverageMpg;
        }

        return mpg;
    }

    /// <summary>The vehicles the filter can offer, archived ones included [1m].</summary>
    public async Task<IReadOnlyList<(Guid Id, string Nickname)>> ListVehiclesAsync(CancellationToken cancellationToken = default)
    {
        var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);
        var all = await vehicles.ListAllAsync(householdId, cancellationToken);
        return [.. all.Select(v => (v.Id, v.Nickname))];
    }

    private (DateOnly From, DateOnly To) ResolveRange(ReportRange range)
    {
        var today = clock.Today;

        return range switch
        {
            ReportRange.YearToDate => (new DateOnly(today.Year, 1, 1), today),
            ReportRange.TwelveMonths => (today.AddMonths(-12), today),
            _ => (DateOnly.MinValue, today)
        };
    }

    private static string Describe(ReportRange range, DateOnly from, DateOnly to) => range switch
    {
        ReportRange.YearToDate => $"{from:MMM} – {to:MMM yyyy}",
        ReportRange.TwelveMonths => $"{from:MMM yyyy} – {to:MMM yyyy}",
        _ => "All time"
    };

    private static IReadOnlyList<CostLine> Filter(IReadOnlyList<CostLine> lines, Guid? vehicleId) =>
        vehicleId is { } id ? [.. lines.Where(l => l.VehicleId == id)] : lines;

    private static IReadOnlyList<CostLine> Sort(IReadOnlyList<CostLine> lines, HistorySort sort, bool descending)
    {
        IOrderedEnumerable<CostLine> ordered = sort switch
        {
            HistorySort.Vehicle => descending
                ? lines.OrderByDescending(l => l.VehicleNickname)
                : lines.OrderBy(l => l.VehicleNickname),
            HistorySort.Odometer => descending
                ? lines.OrderByDescending(l => l.Odometer)
                : lines.OrderBy(l => l.Odometer),
            HistorySort.Category => descending
                ? lines.OrderByDescending(CategoryLabel)
                : lines.OrderBy(CategoryLabel),
            HistorySort.Item => descending
                ? lines.OrderByDescending(l => l.Item)
                : lines.OrderBy(l => l.Item),
            HistorySort.Shop => descending
                ? lines.OrderByDescending(l => l.Shop ?? string.Empty)
                : lines.OrderBy(l => l.Shop ?? string.Empty),
            HistorySort.Cost => descending
                ? lines.OrderByDescending(l => l.Cost)
                : lines.OrderBy(l => l.Cost),
            _ => descending
                ? lines.OrderByDescending(l => l.Date)
                : lines.OrderBy(l => l.Date)
        };

        return [.. ordered.ThenByDescending(l => l.Date)];
    }

    /// <summary>The label the table and the category filter share.</summary>
    public static string CategoryLabel(CostLine line) => line.Kind switch
    {
        CostKind.Fuel => "Fuel",
        _ => line.Category switch
        {
            Domain.ServiceCategory.ScheduledService => "Scheduled service",
            Domain.ServiceCategory.Repair => "Repairs",
            Domain.ServiceCategory.Tires => "Tires",
            Domain.ServiceCategory.Inspection => "Inspection",
            _ => "Other"
        }
    };

    /// <summary>Quotes only where a field would otherwise break the row.</summary>
    private static string Escape(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
