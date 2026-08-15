namespace Garage.Application.Vehicles;

/// <summary>The two identifiers the add-vehicle screen accepts [1b].</summary>
public enum LookupMethod
{
    Vin = 0,
    Plate = 1
}

/// <summary>
/// What a lookup came back with. A failure is not an error condition — story V-1 wants
/// the user dropped into manual entry with whatever was already typed still on screen —
/// so the failure reason travels as a message rather than an exception.
/// </summary>
public record VehicleLookupResult
{
    public required bool Found { get; init; }

    /// <summary>Set when the lookup did not find the vehicle; shown above the manual fields.</summary>
    public string? Message { get; init; }

    public int? Year { get; init; }
    public string? Make { get; init; }
    public string? Model { get; init; }
    public string? Trim { get; init; }
    public string? Engine { get; init; }
    public string? Vin { get; init; }
    public string? LicensePlate { get; init; }

    public static VehicleLookupResult Failed(string message) => new() { Found = false, Message = message };
}
