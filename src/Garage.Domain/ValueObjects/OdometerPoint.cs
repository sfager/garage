namespace Garage.Domain.ValueObjects;

/// <summary>A dated odometer value, for working out how far a vehicle went in a range.</summary>
public record OdometerPoint(Guid VehicleId, DateOnly Date, int Odometer);