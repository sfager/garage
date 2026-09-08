namespace Garage.Domain.ValueObjects;

/// <summary>
/// What a delete would destroy. Story V-4 wants the confirmation to state what is lost
/// rather than asking "are you sure?" into the void.
/// </summary>
public record VehicleDeletionImpact(
    string Nickname,
    int ServiceRecords,
    int FuelEntries,
    int OdometerReadings,
    int Trips,
    int Reminders,
    int Documents,
    decimal TotalSpend)
{
    public int TotalRecords =>
        ServiceRecords + FuelEntries + OdometerReadings + Trips + Reminders + Documents;
}