namespace Garage.Domain.Repositories;

/// <summary>
/// What deleting a vehicle would destroy. Story V-4 wants the confirmation to state what
/// is lost rather than asking "are you sure?" into the void.
///
/// It lives beside <see cref="IVehicleRepository"/> because that contract returns it, and
/// a Domain contract may only speak in Domain types.
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
