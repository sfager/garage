using Garage.Domain;

namespace Garage.Application.ServiceLogging;

/// <summary>One job on the visit. A job picked from a due item remembers which one.</summary>
public class ServiceDraftItem
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Set when this job closes out a reminder, so saving can reschedule it.</summary>
    public Guid? ReminderId { get; set; }

    /// <summary>Story L-3: the interval to reschedule with, editable before saving.</summary>
    public int? NextMileageInterval { get; set; }

    public int? NextMonthInterval { get; set; }
}

/// <summary>
/// An in-progress service entry. Held across the wizard's three routed steps and
/// persisted, so leaving the flow and coming back resumes it (story L-4).
/// </summary>
public class ServiceDraft
{
    public Guid VehicleId { get; set; }

    public List<ServiceDraftItem> Items { get; set; } = [];

    public DateOnly Date { get; set; }
    public int? Odometer { get; set; }
    public ServiceCategory Category { get; set; } = ServiceCategory.ScheduledService;

    public decimal? TotalCost { get; set; }
    public decimal? PartsCost { get; set; }
    public decimal? LaborCost { get; set; }
    public string? Shop { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// Storage keys of receipts already uploaded. Files are written as they are picked
    /// so the draft survives a restart; the Document rows are created on save.
    /// </summary>
    public List<ReceiptDraft> Receipts { get; set; } = [];

    public DateTimeOffset StartedUtc { get; set; } = DateTimeOffset.UtcNow;

    public bool HasItems => Items.Count > 0;

    /// <summary>Whether there is anything worth offering to resume.</summary>
    public bool IsMeaningful => HasItems || TotalCost is not null || !string.IsNullOrWhiteSpace(Notes);

    public string Summary => Items.Count switch
    {
        0 => "Service",
        1 => Items[0].Name,
        _ => $"{Items[0].Name} + {Items.Count - 1} more"
    };
}

public class ReceiptDraft
{
    public string StorageKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}
