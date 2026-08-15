using Garage.Application.Abstractions;
using Garage.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Garage.Infrastructure.Persistence.Repositories;

public class MileageRepository(GarageDbContext context) : IMileageRepository
{
    public async Task<IReadOnlyList<OdometerReading>> ListReadingsAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        await context.OdometerReadings
            .AsNoTracking()
            .Where(r => r.VehicleId == vehicleId)
            .OrderBy(r => r.Date).ThenBy(r => r.Odometer)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Trip>> ListTripsAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        await context.Trips
            .AsNoTracking()
            .Where(t => t.VehicleId == vehicleId)
            .OrderBy(t => t.Date).ThenBy(t => t.EndOdometer)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Unions every table that carries an odometer value. Services and fill-ups count
    /// as evidence of movement even though they are not mileage entries in their own right.
    /// </summary>
    public async Task<IReadOnlyList<(DateOnly Date, int Odometer, bool IsReading)>> ListPointsAsync(
        Guid vehicleId,
        CancellationToken cancellationToken = default)
    {
        var readings = context.OdometerReadings
            .Where(r => r.VehicleId == vehicleId)
            .Select(r => new { r.Date, r.Odometer, IsReading = true });

        var trips = context.Trips
            .Where(t => t.VehicleId == vehicleId)
            .Select(t => new { t.Date, Odometer = t.EndOdometer, IsReading = false });

        var services = context.ServiceRecords
            .Where(s => s.VehicleId == vehicleId)
            .Select(s => new { s.Date, s.Odometer, IsReading = false });

        var fuel = context.FuelEntries
            .Where(f => f.VehicleId == vehicleId)
            .Select(f => new { f.Date, f.Odometer, IsReading = false });

        var points = await readings
            .Concat(trips)
            .Concat(services)
            .Concat(fuel)
            .OrderBy(p => p.Date)
            .ToListAsync(cancellationToken);

        return [.. points.Select(p => (p.Date, p.Odometer, p.IsReading))];
    }
}
