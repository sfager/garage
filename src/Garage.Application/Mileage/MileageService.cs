using System.ComponentModel.DataAnnotations;
using Garage.Application.Abstractions;
using Garage.Domain;
using Garage.Domain.Common;
using Garage.Domain.Entities;
using Garage.Domain.Services;

namespace Garage.Application.Mileage;

/// <summary>Story M-2: a trip is entered either as a distance or as a start/end pair.</summary>
public enum TripEntryMode
{
    Distance = 0,
    StartAndEnd = 1
}

public class RecordReadingRequest
{
    [Required(ErrorMessage = "Enter the odometer reading.")]
    [Range(0, 3_000_000, ErrorMessage = "That odometer reading does not look right.")]
    public int? Odometer { get; set; }

    public DateOnly Date { get; set; }

    [StringLength(400)]
    public string? Note { get; set; }
}

public class RecordTripRequest
{
    public DateOnly Date { get; set; }

    public TripEntryMode Mode { get; set; } = TripEntryMode.Distance;

    [Range(1, 100_000, ErrorMessage = "A trip has to cover at least one mile.")]
    public int? Distance { get; set; }

    [Range(0, 3_000_000)]
    public int? StartOdometer { get; set; }

    [Range(0, 3_000_000)]
    public int? EndOdometer { get; set; }

    [Required(ErrorMessage = "Give the trip a label so you can recognise it later.")]
    [StringLength(120)]
    public string Label { get; set; } = string.Empty;

    public TripPurpose Purpose { get; set; } = TripPurpose.Personal;
}

/// <summary>Epic E2. Odometer readings, trips, and the summary both feed off.</summary>
public class MileageService(
    IVehicleRepository vehicles,
    IMileageRepository mileage,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    /// <summary>
    /// Story M-1. A reading below the current odometer is refused by the aggregate with
    /// a message naming both values, which the UI shows as-is.
    /// </summary>
    public async Task<OdometerReading> RecordReadingAsync(
        Guid vehicleId,
        RecordReadingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Odometer is not { } odometer)
        {
            throw new DomainException("Enter the odometer reading.");
        }

        var vehicle = await RequireAsync(vehicleId, cancellationToken);
        GuardNotFuture(request.Date);

        var reading = vehicle.RecordReading(request.Date, odometer, request.Note);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return reading;
    }

    /// <summary>Story M-2. Trips advance the vehicle's odometer.</summary>
    public async Task<Trip> RecordTripAsync(
        Guid vehicleId,
        RecordTripRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var vehicle = await RequireAsync(vehicleId, cancellationToken);
        GuardNotFuture(request.Date);

        var trip = request.Mode switch
        {
            TripEntryMode.Distance => Trip.FromDistance(
                vehicle.Id,
                request.Date,
                vehicle.CurrentOdometer,
                request.Distance ?? throw new DomainException("Enter how far the trip covered."),
                request.Label,
                request.Purpose),

            TripEntryMode.StartAndEnd => Trip.FromOdometers(
                vehicle.Id,
                request.Date,
                request.StartOdometer ?? throw new DomainException("Enter the starting odometer."),
                request.EndOdometer ?? throw new DomainException("Enter the ending odometer."),
                request.Label,
                request.Purpose),

            _ => throw new DomainException("Choose how to enter the trip.")
        };

        vehicle.RecordTrip(trip);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return trip;
    }

    /// <summary>The merged, newest-first log of readings and trips [1j].</summary>
    public async Task<IReadOnlyList<MileageLogEntry>> GetLogAsync(
        Guid vehicleId,
        MileageEntryKind? filter = null,
        CancellationToken cancellationToken = default)
    {
        await RequireAsync(vehicleId, cancellationToken);

        var entries = new List<MileageLogEntry>();

        if (filter is null or MileageEntryKind.Reading)
        {
            var readings = await mileage.ListReadingsAsync(vehicleId, cancellationToken);
            entries.AddRange(readings.Select(MileageLogEntry.FromReading));
        }

        if (filter is null or MileageEntryKind.Trip)
        {
            var trips = await mileage.ListTripsAsync(vehicleId, cancellationToken);
            entries.AddRange(trips.Select(MileageLogEntry.FromTrip));
        }

        // Oldest first to compute each row's delta, then flipped for display.
        var ordered = entries
            .OrderBy(e => e.Odometer)
            .ThenBy(e => e.Date)
            .ToList();

        var withDeltas = new List<MileageLogEntry>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var entry = ordered[i];

            // A trip already knows its own distance; a reading's delta only exists
            // once there is an earlier entry to measure from.
            var delta = entry.Delta ?? (i == 0 ? null : entry.Odometer - ordered[i - 1].Odometer);
            withDeltas.Add(entry with { Delta = delta });
        }

        withDeltas.Reverse();
        return withDeltas;
    }

    /// <summary>Story M-3, and the source of the rate maintenance projects with.</summary>
    public async Task<MileageSummary> GetSummaryAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        await RequireAsync(vehicleId, cancellationToken);

        var points = await mileage.ListPointsAsync(vehicleId, cancellationToken);
        return MileageCalculator.Summarize(
            points.Select(p => new MileagePoint(p.Date, p.Odometer, p.IsReading)),
            clock.Today);
    }

    private void GuardNotFuture(DateOnly date)
    {
        if (date > clock.Today)
        {
            throw new DomainException("That date is in the future.");
        }
    }

    private async Task<Vehicle> RequireAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);
        return await vehicles.GetForHouseholdAsync(vehicleId, householdId, cancellationToken)
            ?? throw new DomainException("That vehicle is not in your garage.");
    }
}
