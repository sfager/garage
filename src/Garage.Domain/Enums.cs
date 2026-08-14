namespace Garage.Domain;

/// <summary>How an odometer value reached the system.</summary>
public enum OdometerSource
{
    Manual = 0,
    Trip = 1,
    ServiceRecord = 2,
    FuelEntry = 3,
    VehicleSetup = 4
}

public enum TripPurpose
{
    Personal = 0,
    Business = 1
}

/// <summary>Buckets the reports and history table group spend by.</summary>
public enum ServiceCategory
{
    ScheduledService = 0,
    Repair = 1,
    Tires = 2,
    Inspection = 3,
    Other = 4
}

/// <summary>The three bands of wireframe 1c.</summary>
public enum DueBand
{
    Overdue = 0,
    DueSoon = 1,
    Later = 2
}

/// <summary>Which of a reminder's two triggers is projected to fire first.</summary>
public enum DueTrigger
{
    None = 0,
    Mileage = 1,
    Time = 2
}

public enum DocumentType
{
    Insurance = 0,
    Registration = 1,
    Title = 2,
    Inspection = 3,
    Receipt = 4,
    Other = 5
}
