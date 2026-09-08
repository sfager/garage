namespace Garage.Domain.Repositories;

/// <summary>Which side of the ledger a cost came from.</summary>
public enum CostKind
{
    Service = 0,
    Fuel = 1
}

/// <summary>
/// One spend line, flattened from either a service record or a fill-up. Reports need
/// both in a single sequence: the history table lists them together [1m], and every
/// dashboard figure sums across both.
/// </summary>
public record CostLine(
    Guid VehicleId,
    string VehicleNickname,
    Guid RecordId,
    DateOnly Date,
    int Odometer,
    CostKind Kind,
    ServiceCategory Category,
    string Item,
    string? Shop,
    decimal Cost);

/// <summary>A dated odometer value, for working out how far a vehicle went in a range.</summary>
public record OdometerPoint(Guid VehicleId, DateOnly Date, int Odometer);

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
