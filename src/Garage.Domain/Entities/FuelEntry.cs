using Garage.Domain.Common;

namespace Garage.Domain.Entities;

/// <summary>
/// One fill-up. Partial fills are flagged because MPG can only be computed across
/// a tank that was filled to the same point at both ends (story G-2).
/// </summary>
public class FuelEntry : Entity
{
    private FuelEntry() { }

    public FuelEntry(Guid vehicleId, DateOnly date, int odometer, decimal gallons, decimal totalCost, bool isPartialFill = false)
    {
        if (odometer < 0)
        {
            throw new DomainException("An odometer reading cannot be negative.");
        }

        if (gallons <= 0)
        {
            throw new DomainException("A fill-up needs a volume greater than zero.");
        }

        if (totalCost < 0)
        {
            throw new DomainException("A fuel cost cannot be negative.");
        }

        VehicleId = vehicleId;
        Date = date;
        Odometer = odometer;
        Gallons = gallons;
        TotalCost = totalCost;
        IsPartialFill = isPartialFill;
    }

    public Guid VehicleId { get; private set; }
    public Vehicle? Vehicle { get; private set; }

    public DateOnly Date { get; private set; }
    public int Odometer { get; private set; }
    public decimal Gallons { get; private set; }
    public decimal TotalCost { get; private set; }
    public string? Station { get; private set; }
    public bool IsPartialFill { get; private set; }
    public DateTimeOffset CreatedUtc { get; private set; } = DateTimeOffset.UtcNow;

    public decimal PricePerGallon => Gallons == 0 ? 0m : TotalCost / Gallons;

    public void SetStation(string? station) =>
        Station = string.IsNullOrWhiteSpace(station) ? null : station.Trim();
}
