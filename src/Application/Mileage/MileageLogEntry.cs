using Garage.Domain;
using Garage.Domain.Entities;

namespace Garage.Application.Mileage;

/// <summary>Story M-2: readings and trips share one log, filterable by type.</summary>
public enum MileageEntryKind
{
    Reading = 0,
    Trip = 1
}

/// <summary>
/// One row of the mileage log [1j]. <see cref="Delta"/> is the distance covered since
/// the previous entry, so it only exists once there is something to compare against.
/// </summary>
public record MileageLogEntry(
    Guid Id,
    DateOnly Date,
    MileageEntryKind Kind,
    int Odometer,
    int? Delta,
    string Description,
    string? Detail)
{
    public static MileageLogEntry FromReading(OdometerReading reading) => new(
        reading.Id,
        reading.Date,
        MileageEntryKind.Reading,
        reading.Odometer,
        null,
        "reading",
        reading.Source switch
        {
            OdometerSource.VehicleSetup => "starting odometer",
            OdometerSource.Manual => reading.Note ?? "manual",
            _ => reading.Source.ToString().ToLowerInvariant()
        });

    public static MileageLogEntry FromTrip(Trip trip) => new(
        trip.Id,
        trip.Date,
        MileageEntryKind.Trip,
        trip.EndOdometer,
        trip.Distance,
        "trip",
        $"{trip.Label} ({trip.Purpose.ToString().ToLowerInvariant()})");
}
