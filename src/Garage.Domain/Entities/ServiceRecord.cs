using Garage.Domain.Common;

namespace Garage.Domain.Entities;

/// <summary>
/// One visit to a shop (or one driveway afternoon). A visit can cover several jobs,
/// which is why the items are a collection rather than a single description.
/// </summary>
public class ServiceRecord : Entity
{
    private readonly List<ServiceRecordItem> _items = [];
    private readonly List<Document> _receipts = [];

    private ServiceRecord() { }

    public ServiceRecord(Guid vehicleId, DateOnly date, int odometer, ServiceCategory category, decimal totalCost)
    {
        if (odometer < 0)
        {
            throw new DomainException("An odometer reading cannot be negative.");
        }

        if (totalCost < 0)
        {
            throw new DomainException("A total cost cannot be negative.");
        }

        VehicleId = vehicleId;
        Date = date;
        Odometer = odometer;
        Category = category;
        TotalCost = totalCost;
    }

    public Guid VehicleId { get; private set; }
    public Vehicle? Vehicle { get; private set; }

    public DateOnly Date { get; private set; }
    public int Odometer { get; private set; }
    public ServiceCategory Category { get; private set; }
    public decimal TotalCost { get; private set; }
    public decimal? PartsCost { get; private set; }
    public decimal? LaborCost { get; private set; }
    public string? Shop { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedUtc { get; private set; } = DateTimeOffset.UtcNow;

    public IReadOnlyCollection<ServiceRecordItem> Items => _items;
    public IReadOnlyCollection<Document> Receipts => _receipts;

    /// <summary>"Oil &amp; filter + 2 more", for list rows that have one line to work with.</summary>
    public string Summary => _items.Count switch
    {
        0 => Category.ToString(),
        1 => _items[0].Name,
        _ => $"{_items[0].Name} + {_items.Count - 1} more"
    };

    public ServiceRecordItem AddItem(string name, Guid? reminderId = null)
    {
        var item = new ServiceRecordItem(Id, name, reminderId);
        _items.Add(item);
        return item;
    }

    public void ClearItems() => _items.Clear();

    /// <summary>Story L-2: parts and labour are optional, but together may not exceed the total.</summary>
    public void SetCostBreakdown(decimal? partsCost, decimal? laborCost)
    {
        if (partsCost < 0 || laborCost < 0)
        {
            throw new DomainException("Parts and labour cannot be negative.");
        }

        if ((partsCost ?? 0m) + (laborCost ?? 0m) > TotalCost)
        {
            throw new DomainException("Parts and labour add up to more than the total cost.");
        }

        PartsCost = partsCost;
        LaborCost = laborCost;
    }

    public void SetShop(string? shop) =>
        Shop = string.IsNullOrWhiteSpace(shop) ? null : shop.Trim();

    public void SetNotes(string? notes) =>
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

    public void SetTotalCost(decimal totalCost)
    {
        if (totalCost < 0)
        {
            throw new DomainException("A total cost cannot be negative.");
        }

        if ((PartsCost ?? 0m) + (LaborCost ?? 0m) > totalCost)
        {
            throw new DomainException("Parts and labour add up to more than the total cost.");
        }

        TotalCost = totalCost;
    }
}

/// <summary>One job on a visit, optionally the fulfilment of a reminder.</summary>
public class ServiceRecordItem : Entity
{
    private ServiceRecordItem() { }

    internal ServiceRecordItem(Guid serviceRecordId, string name, Guid? reminderId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("A service item needs a name.");
        }

        ServiceRecordId = serviceRecordId;
        Name = name.Trim();
        ReminderId = reminderId;
    }

    public Guid ServiceRecordId { get; private set; }
    public ServiceRecord? ServiceRecord { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Set when this job closed out a scheduled reminder, so it can be rescheduled.</summary>
    public Guid? ReminderId { get; private set; }
    public Reminder? Reminder { get; private set; }
}
