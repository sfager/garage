using Garage.Domain.Common;

namespace Garage.Domain.Entities;

/// <summary>
/// A journey, entered either as start/end odometer or as a distance from the
/// current reading. Either way it lands as a start/end pair so the log reads consistently.
/// </summary>
public class Trip : Entity
{
    private Trip() { }

    private Trip(Guid vehicleId, DateOnly date, int startOdometer, int endOdometer, string label, TripPurpose purpose)
    {
        if (endOdometer <= startOdometer)
        {
            throw new DomainException("A trip has to cover at least one mile.");
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            throw new DomainException("Give the trip a label so you can recognise it later.");
        }

        VehicleId = vehicleId;
        Date = date;
        StartOdometer = startOdometer;
        EndOdometer = endOdometer;
        Label = label.Trim();
        Purpose = purpose;
    }

    public static Trip FromOdometers(Guid vehicleId, DateOnly date, int startOdometer, int endOdometer, string label, TripPurpose purpose) =>
        new(vehicleId, date, startOdometer, endOdometer, label, purpose);

    public static Trip FromDistance(Guid vehicleId, DateOnly date, int startOdometer, int distance, string label, TripPurpose purpose)
    {
        if (distance <= 0)
        {
            throw new DomainException("A trip has to cover at least one mile.");
        }

        return new Trip(vehicleId, date, startOdometer, startOdometer + distance, label, purpose);
    }

    public Guid VehicleId { get; private set; }
    public Vehicle? Vehicle { get; private set; }

    public DateOnly Date { get; private set; }
    public int StartOdometer { get; private set; }
    public int EndOdometer { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public TripPurpose Purpose { get; private set; }
    public DateTimeOffset CreatedUtc { get; private set; } = DateTimeOffset.UtcNow;

    public int Distance => EndOdometer - StartOdometer;
}
