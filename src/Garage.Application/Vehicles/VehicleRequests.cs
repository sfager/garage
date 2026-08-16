using System.ComponentModel.DataAnnotations;

namespace Garage.Application.Vehicles;

/// <summary>Step 2 of the add flow [1b]: confirm the details and set the starting odometer.</summary>
public class AddVehicleRequest
{
    [Required(ErrorMessage = "Give the vehicle a nickname.")]
    [StringLength(60)]
    public string Nickname { get; set; } = string.Empty;

    /// <summary>Story V-2: required, because mileage tracking has to start from a real number.</summary>
    [Required(ErrorMessage = "Enter today's odometer reading.")]
    [Range(0, 3_000_000, ErrorMessage = "That odometer reading does not look right.")]
    public int? Odometer { get; set; }

    [Range(1885, 2200, ErrorMessage = "That model year does not look right.")]
    public int? Year { get; set; }

    [StringLength(60)] public string? Make { get; set; }
    [StringLength(60)] public string? Model { get; set; }
    [StringLength(60)] public string? Trim { get; set; }
    [StringLength(60)] public string? Engine { get; set; }
    [StringLength(17, MinimumLength = 0)] public string? Vin { get; set; }
    [StringLength(12)] public string? LicensePlate { get; set; }
}

/// <summary>Story V-4: edit covers nickname, details and photo.</summary>
public class EditVehicleRequest
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "Give the vehicle a nickname.")]
    [StringLength(60)]
    public string Nickname { get; set; } = string.Empty;

    [Range(1885, 2200, ErrorMessage = "That model year does not look right.")]
    public int? Year { get; set; }

    [StringLength(60)] public string? Make { get; set; }
    [StringLength(60)] public string? Model { get; set; }
    [StringLength(60)] public string? Trim { get; set; }
    [StringLength(60)] public string? Engine { get; set; }
    [StringLength(17)] public string? Vin { get; set; }
    [StringLength(12)] public string? LicensePlate { get; set; }
}
