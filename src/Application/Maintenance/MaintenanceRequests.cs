using System.ComponentModel.DataAnnotations;
using Garage.Domain;
using Garage.Domain.Services;

namespace Garage.Application.Maintenance;

/// <summary>Story S-1, matching the fields of wireframe 1k.</summary>
public class ReminderRequest
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Say what the reminder is for.")]
    [StringLength(120)]
    public string Item { get; set; } = string.Empty;

    [Range(1, 500_000, ErrorMessage = "The mileage interval has to be greater than zero.")]
    public int? MileageInterval { get; set; }

    [Range(1, 600, ErrorMessage = "The month interval has to be greater than zero.")]
    public int? MonthInterval { get; set; }

    public bool RepeatAfterService { get; set; } = true;

    public bool NotificationsEnabled { get; set; } = true;

    /// <summary>True when neither interval is filled in, which S-1 forbids.</summary>
    public bool HasNoTrigger => MileageInterval is null && MonthInterval is null;
}

/// <summary>
/// Story S-1: the projected due point, shown before saving so the combined
/// "whichever comes first" rule is visible rather than implied.
/// </summary>
public record ReminderPreview(int? DueOdometer, DateOnly? DueDate, string Explanation);

/// <summary>Story S-4: defer by a distance, a period, or both.</summary>
public class SnoozeRequest
{
    [Range(1, 100_000)] public int? ByMiles { get; set; }

    [Range(1, 120)] public int? ByMonths { get; set; }
}

/// <summary>A reminder plus where it currently stands, for the grouped list [1c].</summary>
public record ReminderCard(
    Guid Id,
    string Item,
    int? MileageInterval,
    int? MonthInterval,
    string IntervalDescription,
    bool RepeatAfterService,
    bool NotificationsEnabled,
    bool IsDismissed,
    ReminderProjection Projection)
{
    public DueBand Band => Projection.Band;

    /// <summary>Reloads the edit form without a round trip to the repository.</summary>
    public ReminderRequest ToRequest() => new()
    {
        Id = Id,
        Item = Item,
        MileageInterval = MileageInterval,
        MonthInterval = MonthInterval,
        RepeatAfterService = RepeatAfterService,
        NotificationsEnabled = NotificationsEnabled
    };
}

/// <summary>Story S-6: one row of the history tab.</summary>
public record ServiceHistoryEntry(
    Guid Id,
    DateOnly Date,
    int Odometer,
    string Summary,
    ServiceCategory Category,
    decimal TotalCost,
    string? Shop,
    int ReceiptCount);
