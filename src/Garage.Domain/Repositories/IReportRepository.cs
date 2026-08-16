using Garage.Domain.ValueObjects;

namespace Garage.Domain.Repositories;

/// <summary>
/// Reads the household's spend and mileage for the reports screen. Defined here in the
/// Domain so the contract sits with the model it describes, and returns only Domain
/// types — the Domain depends on nothing outside itself.
/// </summary>
public interface IReportRepository
{
    /// <summary>Every service and fuel cost in the window, across the household.</summary>
    Task<IReadOnlyList<CostLine>> ListCostLinesAsync(
        Guid householdId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every odometer value in the window, from readings, trips, services and fill-ups,
    /// so miles driven can be measured per vehicle.
    /// </summary>
    Task<IReadOnlyList<OdometerPoint>> ListOdometerPointsAsync(
        Guid householdId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}
