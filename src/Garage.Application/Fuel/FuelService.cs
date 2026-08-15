using Garage.Application.Abstractions;
using Garage.Domain.Common;
using Garage.Domain.Entities;
using Garage.Domain.Services;

namespace Garage.Application.Fuel;

/// <summary>
/// Epic E5. The fuel log, the efficiency figures above it, and the trend the chart draws.
/// Cost per mile counts service alongside fuel, which is what makes it agree with the
/// reports screen [1m] rather than quietly meaning something narrower here.
/// </summary>
public class FuelService(
    IVehicleRepository vehicles,
    IFuelRepository fuel,
    IServiceRecordRepository serviceRecords,
    IMileageRepository mileage,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<FuelScreen> GetScreenAsync(
        Guid vehicleId,
        FuelRange range = FuelRange.SixMonths,
        FuelMetric metric = FuelMetric.Mpg,
        CancellationToken cancellationToken = default)
    {
        await RequireVehicleAsync(vehicleId, cancellationToken);

        var entries = await fuel.ListForVehicleAsync(vehicleId, cancellationToken);
        var efficiency = FuelCalculator.Calculate(entries.Select(ToFill));

        var from = StartOf(range);
        var stats = await BuildStatsAsync(vehicleId, entries, efficiency, from, cancellationToken);

        var serviceSpend = await serviceRecords.ListSpendAsync(vehicleId, from, clock.Today, cancellationToken);
        var trend = BuildTrend(efficiency, serviceSpend, from, metric);
        var comparison = await BuildComparisonAsync(cancellationToken);

        var stations = entries.ToDictionary(e => e.Id, e => e.Station);

        return new FuelScreen(stats, efficiency, trend, comparison, stations);
    }

    /// <summary>Story G-1.</summary>
    public async Task<FuelEntry> SaveAsync(Guid vehicleId, FuelEntryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Odometer is not { } odometer)
        {
            throw new DomainException("Enter the odometer reading.");
        }

        if (request.Gallons is not { } gallons)
        {
            throw new DomainException("Enter how much fuel went in.");
        }

        if (request.TotalCost is not { } cost)
        {
            throw new DomainException("Enter what it cost.");
        }

        if (request.Date > clock.Today)
        {
            throw new DomainException("That date is in the future.");
        }

        var vehicle = await RequireVehicleAsync(vehicleId, cancellationToken);

        if (request.Id is { } existingId)
        {
            var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);
            var existing = await fuel.GetForHouseholdAsync(existingId, householdId, cancellationToken)
                ?? throw new DomainException("That fill-up is not in your garage.");

            fuel.Remove(existing);
        }

        var entry = new FuelEntry(vehicle.Id, request.Date, odometer, gallons, cost, request.IsPartialFill);
        entry.SetStation(request.Station);

        // Advances the vehicle's odometer if this reading is the highest yet.
        vehicle.RecordFillUp(entry);

        await fuel.AddAsync(entry, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return entry;
    }

    public async Task DeleteAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);
        var entry = await fuel.GetForHouseholdAsync(entryId, householdId, cancellationToken)
            ?? throw new DomainException("That fill-up is not in your garage.");

        fuel.Remove(entry);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListStationsAsync(CancellationToken cancellationToken = default)
    {
        var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);
        return await fuel.ListStationsAsync(householdId, cancellationToken);
    }

    /// <summary>The three figures across the top of wireframe 1h.</summary>
    private async Task<FuelStats> BuildStatsAsync(
        Guid vehicleId,
        IReadOnlyList<FuelEntry> entries,
        FuelEfficiency efficiency,
        DateOnly from,
        CancellationToken cancellationToken)
    {
        var today = clock.Today;
        var thirtyDaysAgo = today.AddDays(-30);

        var spend30 = entries.Where(e => e.Date >= thirtyDaysAgo).Sum(e => e.TotalCost)
                      + await serviceRecords.SumSpendAsync(vehicleId, thirtyDaysAgo, today, cancellationToken);

        var fuelSpend = entries.Where(e => e.Date >= from).Sum(e => e.TotalCost);
        var serviceSpend = await serviceRecords.SumSpendAsync(vehicleId, from, today, cancellationToken);
        var miles = await MilesDrivenAsync(vehicleId, from, today, cancellationToken);

        decimal? costPerMile = miles > 0 ? (fuelSpend + serviceSpend) / miles : null;

        return new FuelStats(
            efficiency.AverageMpg,
            efficiency.UnavailableReason,
            costPerMile,
            costPerMile is null ? "Not enough mileage recorded in this range yet." : null,
            spend30,
            miles,
            fuelSpend,
            serviceSpend);
    }

    /// <summary>
    /// Distance covered between the first and last odometer reading inside the window.
    /// Anything before it is another period's mileage.
    /// </summary>
    private async Task<int> MilesDrivenAsync(Guid vehicleId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var points = await mileage.ListPointsAsync(vehicleId, cancellationToken);
        var inRange = points.Where(p => p.Date >= from && p.Date <= to).ToList();

        return inRange.Count < 2 ? 0 : inRange.Max(p => p.Odometer) - inRange.Min(p => p.Odometer);
    }

    /// <summary>
    /// Story G-3: monthly buckets of whichever metric is toggled on [1i]. Cost per mile
    /// and spend count service alongside fuel, so they mean the same thing here as in the
    /// stats strip above them and on the reports screen — the epic is running costs, not
    /// fuel costs.
    /// </summary>
    private static List<FuelTrendPoint> BuildTrend(
        FuelEfficiency efficiency,
        IReadOnlyList<(DateOnly Date, decimal Cost)> serviceSpend,
        DateOnly from,
        FuelMetric metric)
    {
        static DateOnly MonthOf(DateOnly date) => new(date.Year, date.Month, 1);

        var fillsByMonth = efficiency.Fills
            .Where(f => f.Date >= from)
            .GroupBy(f => MonthOf(f.Date))
            .ToDictionary(g => g.Key, g => g.ToList());

        var serviceByMonth = serviceSpend
            .GroupBy(s => MonthOf(s.Date))
            .ToDictionary(g => g.Key, g => g.Sum(s => s.Cost));

        // Spend is meaningful in a month with service and no fuel; the other two metrics
        // need measured fuel miles, so a service-only month would just be an empty column.
        var months = fillsByMonth.Keys
            .Union(metric == FuelMetric.Spend ? serviceByMonth.Keys : [])
            .OrderBy(m => m)
            .ToList();

        var points = new List<FuelTrendPoint>();

        foreach (var month in months)
        {
            var fills = fillsByMonth.GetValueOrDefault(month, []);
            var measured = fills.Where(f => f.Mpg is not null).ToList();
            var service = serviceByMonth.GetValueOrDefault(month, 0m);
            var milesMeasured = measured.Sum(f => f.MilesCovered!.Value);

            double? value = metric switch
            {
                // Aggregate within the month for the same reason the average aggregates.
                FuelMetric.Mpg when measured.Count > 0 =>
                    milesMeasured / (double)measured.Sum(f => f.Gallons),

                FuelMetric.CostPerMile when milesMeasured > 0 =>
                    (double)(measured.Sum(f => f.TotalCost) + service) / milesMeasured,

                FuelMetric.Spend => (double)(fills.Sum(f => f.TotalCost) + service),

                _ => null
            };

            var label = metric switch
            {
                FuelMetric.Mpg => value is { } v ? $"{v:N1}" : "—",
                FuelMetric.CostPerMile => value is { } v ? $"${v:N2}" : "—",
                _ => value is { } v ? $"${v:N0}" : "—"
            };

            points.Add(new FuelTrendPoint(month, value, label));
        }

        return points;
    }

    /// <summary>Story G-3: both vehicles comparable in one block [1i].</summary>
    private async Task<List<VehicleEfficiency>> BuildComparisonAsync(CancellationToken cancellationToken)
    {
        var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);
        var all = await vehicles.ListActiveAsync(householdId, cancellationToken);
        var from = StartOf(FuelRange.TwelveMonths);
        var today = clock.Today;

        var comparison = new List<VehicleEfficiency>();

        foreach (var vehicle in all)
        {
            var entries = await fuel.ListForVehicleAsync(vehicle.Id, cancellationToken);
            var efficiency = FuelCalculator.Calculate(entries.Select(ToFill));

            var fuelSpend = entries.Where(e => e.Date >= from).Sum(e => e.TotalCost);
            var serviceSpend = await serviceRecords.SumSpendAsync(vehicle.Id, from, today, cancellationToken);
            var miles = await MilesDrivenAsync(vehicle.Id, from, today, cancellationToken);

            comparison.Add(new VehicleEfficiency(
                vehicle.Id,
                vehicle.Nickname,
                efficiency.AverageMpg,
                miles > 0 ? (fuelSpend + serviceSpend) / miles : null));
        }

        return comparison;
    }

    private DateOnly StartOf(FuelRange range) => range switch
    {
        FuelRange.SixMonths => clock.Today.AddMonths(-6),
        FuelRange.TwelveMonths => clock.Today.AddMonths(-12),
        _ => DateOnly.MinValue
    };

    private static FuelFill ToFill(FuelEntry entry) =>
        new(entry.Id, entry.Date, entry.Odometer, entry.Gallons, entry.TotalCost, entry.IsPartialFill);

    private async Task<Vehicle> RequireVehicleAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);
        return await vehicles.GetForHouseholdAsync(vehicleId, householdId, cancellationToken)
            ?? throw new DomainException("That vehicle is not in your garage.");
    }
}
