using System.ComponentModel.DataAnnotations;
using Garage.Domain.Services;

namespace Garage.Application.Fuel;

/// <summary>Story G-1, matching the fields of wireframe 1h.</summary>
public class FuelEntryRequest
{
    public Guid? Id { get; set; }

    public DateOnly Date { get; set; }

    [Required(ErrorMessage = "Enter the odometer reading.")]
    [Range(0, 3_000_000, ErrorMessage = "That odometer reading does not look right.")]
    public int? Odometer { get; set; }

    [Required(ErrorMessage = "Enter how much fuel went in.")]
    [Range(0.01, 200, ErrorMessage = "That volume does not look right.")]
    public decimal? Gallons { get; set; }

    [Required(ErrorMessage = "Enter what it cost.")]
    [Range(0, 10_000, ErrorMessage = "That cost does not look right.")]
    public decimal? TotalCost { get; set; }

    [StringLength(120)]
    public string? Station { get; set; }

    /// <summary>Excluded from the MPG calculation in its own right (story G-2).</summary>
    public bool IsPartialFill { get; set; }
}

/// <summary>
/// The stats strip of wireframe 1h. Every figure is nullable with a reason beside it,
/// because G-2 forbids showing a zero where the answer is "not yet".
/// </summary>
public record FuelStats(
    double? AverageMpg,
    string? MpgUnavailableReason,
    decimal? CostPerMile,
    string? CostPerMileUnavailableReason,
    decimal SpendLast30Days,
    int MilesInRange,
    decimal FuelSpendInRange,
    decimal ServiceSpendInRange);

/// <summary>What the trend chart plots (story G-3).</summary>
public enum FuelMetric
{
    Mpg = 0,
    CostPerMile = 1,
    Spend = 2
}

public enum FuelRange
{
    SixMonths = 0,
    TwelveMonths = 1,
    All = 2
}

/// <summary>One column of the trend chart.</summary>
public record FuelTrendPoint(DateOnly Month, double? Value, string Label);

/// <summary>One line of the both-vehicles block in wireframe 1i.</summary>
public record VehicleEfficiency(Guid VehicleId, string Nickname, double? AverageMpg, decimal? CostPerMile);

/// <summary>Everything the fuel screen renders in one load.</summary>
public record FuelScreen(
    FuelStats Stats,
    FuelEfficiency Efficiency,
    IReadOnlyList<FuelTrendPoint> Trend,
    IReadOnlyList<VehicleEfficiency> Comparison,
    IReadOnlyDictionary<Guid, string?> Stations);
