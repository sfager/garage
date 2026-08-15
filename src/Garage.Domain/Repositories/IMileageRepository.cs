using Garage.Domain.Entities;

namespace Garage.Domain.Repositories;

public interface IMileageRepository
{
    Task<IReadOnlyList<OdometerReading>> ListReadingsAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Trip>> ListTripsAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every dated odometer value the vehicle has, from readings, trips, services and
    /// fill-ups alike, so the daily average reflects all known movement. Readings are
    /// flagged because "miles since last" measures from one reading to the next.
    /// </summary>
    Task<IReadOnlyList<(DateOnly Date, int Odometer, bool IsReading)>> ListPointsAsync(Guid vehicleId, CancellationToken cancellationToken = default);
}
