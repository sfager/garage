namespace Garage.Domain.ValueObjects;

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

