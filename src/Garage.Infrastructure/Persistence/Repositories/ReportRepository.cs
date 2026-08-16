using Garage.Domain;
using Garage.Domain.Repositories;
using Garage.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Garage.Infrastructure.Persistence.Repositories;

public class ReportRepository(GarageDbContext context) : IReportRepository
{
    /// <summary>
    /// Service records and fill-ups flattened into one sequence. A service contributes
    /// its item summary; a fill-up contributes its volume, which is what reads usefully
    /// in the history table's Item column [1m].
    /// </summary>
    public async Task<IReadOnlyList<CostLine>> ListCostLinesAsync(
        Guid householdId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var services = await context.ServiceRecords
            .Where(s => s.Vehicle!.HouseholdId == householdId && s.Date >= from && s.Date <= to)
            .Include(s => s.Items)
            .Select(s => new
            {
                s.VehicleId,
                Nickname = s.Vehicle!.Nickname,
                s.Id,
                s.Date,
                s.Odometer,
                s.Category,
                Items = s.Items.Select(i => i.Name).ToList(),
                s.Shop,
                s.TotalCost
            })
            .ToListAsync(cancellationToken);

        var fuel = await context.FuelEntries
            .Where(f => f.Vehicle!.HouseholdId == householdId && f.Date >= from && f.Date <= to)
            .Select(f => new
            {
                f.VehicleId,
                Nickname = f.Vehicle!.Nickname,
                f.Id,
                f.Date,
                f.Odometer,
                f.Gallons,
                Station = f.Station,
                f.TotalCost
            })
            .ToListAsync(cancellationToken);

        var lines = new List<CostLine>(services.Count + fuel.Count);

        lines.AddRange(services.Select(s => new CostLine(
            s.VehicleId,
            s.Nickname,
            s.Id,
            s.Date,
            s.Odometer,
            CostKind.Service,
            s.Category,
            Describe(s.Items, s.Category),
            s.Shop,
            s.TotalCost)));

        lines.AddRange(fuel.Select(f => new CostLine(
            f.VehicleId,
            f.Nickname,
            f.Id,
            f.Date,
            f.Odometer,
            CostKind.Fuel,
            ServiceCategory.Other,
            $"{f.Gallons:N1} gal",
            f.Station,
            f.TotalCost)));

        return [.. lines.OrderByDescending(l => l.Date).ThenByDescending(l => l.Cost)];
    }

    public async Task<IReadOnlyList<OdometerPoint>> ListOdometerPointsAsync(
        Guid householdId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var readings = context.OdometerReadings
            .Where(r => r.Vehicle!.HouseholdId == householdId && r.Date >= from && r.Date <= to)
            .Select(r => new { r.VehicleId, r.Date, r.Odometer });

        var trips = context.Trips
            .Where(t => t.Vehicle!.HouseholdId == householdId && t.Date >= from && t.Date <= to)
            .Select(t => new { t.VehicleId, t.Date, Odometer = t.EndOdometer });

        var services = context.ServiceRecords
            .Where(s => s.Vehicle!.HouseholdId == householdId && s.Date >= from && s.Date <= to)
            .Select(s => new { s.VehicleId, s.Date, s.Odometer });

        var fuel = context.FuelEntries
            .Where(f => f.Vehicle!.HouseholdId == householdId && f.Date >= from && f.Date <= to)
            .Select(f => new { f.VehicleId, f.Date, f.Odometer });

        var points = await readings
            .Concat(trips)
            .Concat(services)
            .Concat(fuel)
            .ToListAsync(cancellationToken);

        return [.. points.Select(p => new OdometerPoint(p.VehicleId, p.Date, p.Odometer))];
    }

    /// <summary>"Oil &amp; filter + 2 more", matching the summary the record itself renders.</summary>
    private static string Describe(List<string> items, ServiceCategory category) => items.Count switch
    {
        0 => category.ToString(),
        1 => items[0],
        _ => $"{items[0]} + {items.Count - 1} more"
    };
}
