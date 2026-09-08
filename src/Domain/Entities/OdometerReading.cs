using Garage.Domain.Common;

namespace Garage.Domain.Entities;

/// <summary>A dated odometer value. Every mileage-bearing record produces one.</summary>
public class OdometerReading : Entity
{
    private OdometerReading() { }

    public OdometerReading(Guid vehicleId, DateOnly date, int odometer, OdometerSource source, string? note = null)
    {
        if (odometer < 0)
        {
            throw new DomainException("An odometer reading cannot be negative.");
        }

        VehicleId = vehicleId;
        Date = date;
        Odometer = odometer;
        Source = source;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public Guid VehicleId { get; private set; }
    public Vehicle? Vehicle { get; private set; }

    public DateOnly Date { get; private set; }
    public int Odometer { get; private set; }
    public OdometerSource Source { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset CreatedUtc { get; private set; } = DateTimeOffset.UtcNow;
}
