using Garage.Domain.Common;

namespace Garage.Domain.Entities;

/// <summary>
/// A car in the garage. The vehicle owns the single running odometer value that
/// every mileage-based projection reads, so it can only ever move forward.
/// </summary>
public class Vehicle : Entity
{
    private readonly List<OdometerReading> _odometerReadings = [];
    private readonly List<Trip> _trips = [];
    private readonly List<ServiceRecord> _serviceRecords = [];
    private readonly List<FuelEntry> _fuelEntries = [];
    private readonly List<Reminder> _reminders = [];
    private readonly List<Document> _documents = [];

    private Vehicle() { }

    public Vehicle(Guid householdId, string nickname, int startingOdometer, DateOnly startingOdometerDate)
    {
        if (householdId == Guid.Empty)
        {
            throw new DomainException("A vehicle must belong to a household.");
        }

        HouseholdId = householdId;
        Rename(nickname);

        if (startingOdometer < 0)
        {
            throw new DomainException("The starting odometer cannot be negative.");
        }

        CurrentOdometer = startingOdometer;
        CurrentOdometerDate = startingOdometerDate;
        _odometerReadings.Add(new OdometerReading(Id, startingOdometerDate, startingOdometer, OdometerSource.VehicleSetup));
    }

    public Guid HouseholdId { get; private set; }
    public Household? Household { get; private set; }

    public string Nickname { get; private set; } = string.Empty;
    public int? Year { get; private set; }
    public string? Make { get; private set; }
    public string? Model { get; private set; }
    public string? Trim { get; private set; }
    public string? Engine { get; private set; }
    public string? Vin { get; private set; }
    public string? LicensePlate { get; private set; }
    public string? PhotoPath { get; private set; }

    /// <summary>Highest odometer value seen across readings, trips, services and fill-ups.</summary>
    public int CurrentOdometer { get; private set; }
    public DateOnly CurrentOdometerDate { get; private set; }

    /// <summary>Archived vehicles drop out of the switcher but stay in reports.</summary>
    public bool IsArchived { get; private set; }
    public DateTimeOffset CreatedUtc { get; private set; } = DateTimeOffset.UtcNow;

    public IReadOnlyCollection<OdometerReading> OdometerReadings => _odometerReadings;
    public IReadOnlyCollection<Trip> Trips => _trips;
    public IReadOnlyCollection<ServiceRecord> ServiceRecords => _serviceRecords;
    public IReadOnlyCollection<FuelEntry> FuelEntries => _fuelEntries;
    public IReadOnlyCollection<Reminder> Reminders => _reminders;
    public IReadOnlyCollection<Document> Documents => _documents;

    /// <summary>"2019 Subaru Outback 2.5i Premium", falling back to the nickname.</summary>
    public string DisplayName
    {
        get
        {
            var parts = new[] { Year?.ToString(), Make, Model, Trim }
                .Where(p => !string.IsNullOrWhiteSpace(p));
            var full = string.Join(' ', parts);
            return string.IsNullOrWhiteSpace(full) ? Nickname : full;
        }
    }

    public void Rename(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
        {
            throw new DomainException("A vehicle needs a nickname.");
        }

        Nickname = nickname.Trim();
    }

    public void SetDetails(int? year, string? make, string? model, string? trim, string? engine, string? vin, string? licensePlate)
    {
        if (year is < 1885 or > 2200)
        {
            throw new DomainException("That model year does not look right.");
        }

        Year = year;
        Make = Normalize(make);
        Model = Normalize(model);
        Trim = Normalize(trim);
        Engine = Normalize(engine);
        Vin = Normalize(vin)?.ToUpperInvariant();
        LicensePlate = Normalize(licensePlate)?.ToUpperInvariant();
    }

    public void SetPhoto(string? photoPath) => PhotoPath = Normalize(photoPath);

    /// <summary>
    /// Moves the car to another household, which happens when its owner joins someone
    /// else's garage — their cars come with them rather than being left unreachable.
    /// </summary>
    public void MoveToHousehold(Guid householdId)
    {
        if (householdId == Guid.Empty)
        {
            throw new DomainException("A vehicle must belong to a household.");
        }

        HouseholdId = householdId;
    }

    public void Archive() => IsArchived = true;

    public void Restore() => IsArchived = false;

    /// <summary>
    /// Records a manual reading. Rejects anything below the current odometer — story M-1
    /// wants a clear message and a correction path rather than a silent overwrite.
    /// </summary>
    public OdometerReading RecordReading(DateOnly date, int odometer, string? note = null)
    {
        GuardForward(odometer);

        var reading = new OdometerReading(Id, date, odometer, OdometerSource.Manual, note);
        _odometerReadings.Add(reading);
        AdvanceOdometer(odometer, date);
        return reading;
    }

    public Trip RecordTrip(Trip trip)
    {
        ArgumentNullException.ThrowIfNull(trip);
        GuardForward(trip.EndOdometer);

        _trips.Add(trip);
        AdvanceOdometer(trip.EndOdometer, trip.Date);
        return trip;
    }

    public ServiceRecord RecordService(ServiceRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        _serviceRecords.Add(record);
        AdvanceOdometer(record.Odometer, record.Date);
        return record;
    }

    public FuelEntry RecordFillUp(FuelEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        _fuelEntries.Add(entry);
        AdvanceOdometer(entry.Odometer, entry.Date);
        return entry;
    }

    public Reminder AddReminder(Reminder reminder)
    {
        ArgumentNullException.ThrowIfNull(reminder);
        _reminders.Add(reminder);
        return reminder;
    }

    public Document AddDocument(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _documents.Add(document);
        return document;
    }

    /// <summary>
    /// Services and fill-ups may legitimately be back-dated below the current reading,
    /// so they only push the odometer up, never down.
    /// </summary>
    private void AdvanceOdometer(int odometer, DateOnly date)
    {
        if (odometer <= CurrentOdometer)
        {
            return;
        }

        CurrentOdometer = odometer;
        CurrentOdometerDate = date;
    }

    private void GuardForward(int odometer)
    {
        if (odometer < CurrentOdometer)
        {
            throw new DomainException(
                $"{odometer:N0} mi is below the last recorded reading of {CurrentOdometer:N0} mi. " +
                "Correct the earlier reading first if that one was wrong.");
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
