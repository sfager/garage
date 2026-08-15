using Garage.Application.Abstractions;
using Garage.Domain;
using Garage.Domain.Common;
using Garage.Domain.Entities;
using Garage.Domain.Repositories;

namespace Garage.Application.ServiceLogging;

/// <summary>
/// Epic E4's three-step wizard [1f]. Holds the draft across routed steps, persists it so
/// an abandoned entry can be resumed (story L-4), and commits it in one transaction.
/// </summary>
public class ServiceLogWizard(
    IVehicleRepository vehicles,
    IServiceRecordRepository serviceRecords,
    IReminderRepository reminders,
    IServiceDraftStore drafts,
    IFileStore fileStore,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    private ServiceDraft? _draft;
    private bool _loaded;

    public ServiceDraft Draft => _draft ??= NewDraft(Guid.Empty);

    /// <summary>
    /// Restores any saved draft. A draft belonging to a different vehicle than the one
    /// now selected is started over rather than silently logged against the wrong car.
    /// </summary>
    public async Task<ServiceDraft> EnsureLoadedAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        if (!_loaded)
        {
            _draft = await drafts.LoadAsync(cancellationToken);
            _loaded = true;
        }

        if (_draft is null || (_draft.VehicleId != vehicleId && vehicleId != Guid.Empty))
        {
            _draft = NewDraft(vehicleId);
        }

        return _draft;
    }

    /// <summary>True when there is a saved entry worth offering to pick back up.</summary>
    public bool HasResumableDraft => _draft?.IsMeaningful == true;

    public async Task StartNewAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        await DiscardStoredFilesAsync(cancellationToken);
        _draft = NewDraft(vehicleId);
        _loaded = true;
        await drafts.SaveAsync(_draft, cancellationToken);
    }

    public Task PersistAsync(CancellationToken cancellationToken = default) =>
        drafts.SaveAsync(Draft, cancellationToken);

    /// <summary>Story L-4: abandoning discards the draft and any receipts already uploaded.</summary>
    public async Task DiscardAsync(CancellationToken cancellationToken = default)
    {
        await DiscardStoredFilesAsync(cancellationToken);
        _draft = null;
        _loaded = false;
        await drafts.ClearAsync(cancellationToken);
    }

    /// <summary>Story L-1: a visit can cover several jobs.</summary>
    public async Task AddItemAsync(string name, Guid? reminderId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Give the job a name.");
        }

        var trimmed = name.Trim();

        if (Draft.Items.Any(i => string.Equals(i.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        Draft.Items.Add(new ServiceDraftItem { Name = trimmed, ReminderId = reminderId });
        Draft.Category = CommonJobs.CategoryFor(Draft.Items.Select(i => i.Name));

        await PersistAsync(cancellationToken);
    }

    public async Task RemoveItemAsync(string name, CancellationToken cancellationToken = default)
    {
        Draft.Items.RemoveAll(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));
        Draft.Category = CommonJobs.CategoryFor(Draft.Items.Select(i => i.Name));
        await PersistAsync(cancellationToken);
    }

    /// <summary>Story L-3: receipts are stored as they are picked so the draft survives a restart.</summary>
    public async Task AddReceiptAsync(
        Stream content,
        string fileName,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken = default)
    {
        var key = await fileStore.SaveAsync(content, fileName, "receipts", cancellationToken);

        Draft.Receipts.Add(new ReceiptDraft
        {
            StorageKey = key,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes
        });

        await PersistAsync(cancellationToken);
    }

    public async Task RemoveReceiptAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        Draft.Receipts.RemoveAll(r => r.StorageKey == storageKey);
        await fileStore.DeleteAsync(storageKey, cancellationToken);
        await PersistAsync(cancellationToken);
    }

    /// <summary>
    /// Story L-3: writes the record, moves the odometer and reschedules every reminder
    /// the visit closed out. One SaveChanges, so a failure leaves nothing half-written.
    /// </summary>
    public async Task<ServiceRecord> SaveAsync(CancellationToken cancellationToken = default)
    {
        var draft = Draft;

        if (!draft.HasItems)
        {
            throw new DomainException("Add at least one job before saving.");
        }

        if (draft.TotalCost is not { } total)
        {
            throw new DomainException("Enter the total cost.");
        }

        if (draft.Odometer is not { } odometer)
        {
            throw new DomainException("Enter the odometer reading.");
        }

        if (draft.Date > clock.Today)
        {
            throw new DomainException("That date is in the future.");
        }

        var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);
        var vehicle = await vehicles.GetForHouseholdAsync(draft.VehicleId, householdId, cancellationToken)
            ?? throw new DomainException("That vehicle is not in your garage.");

        var record = new ServiceRecord(vehicle.Id, draft.Date, odometer, draft.Category, total);
        record.SetCostBreakdown(draft.PartsCost, draft.LaborCost);
        record.SetShop(draft.Shop);
        record.SetNotes(draft.Notes);

        foreach (var item in draft.Items)
        {
            record.AddItem(item.Name, item.ReminderId);
        }

        foreach (var receipt in draft.Receipts)
        {
            var document = new Document(
                vehicle.Id,
                DocumentType.Receipt,
                $"{draft.Summary} — {draft.Date:MMM d, yyyy}",
                receipt.FileName,
                receipt.ContentType,
                receipt.StorageKey,
                receipt.SizeBytes);

            record.AttachReceipt(document);
            vehicle.AddDocument(document);
        }

        // Adds the record and advances the vehicle's odometer if this reading is higher.
        vehicle.RecordService(record);

        await RescheduleRemindersAsync(draft, odometer, cancellationToken);
        await serviceRecords.AddAsync(record, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // The draft is committed, so its files now belong to the record — clear without deleting them.
        _draft = null;
        _loaded = false;
        await drafts.ClearAsync(cancellationToken);

        return record;
    }

    /// <summary>
    /// Story S-4 and L-3: a repeating reminder re-anchors to this service, picking up any
    /// interval the user edited on step 3.
    /// </summary>
    private async Task RescheduleRemindersAsync(ServiceDraft draft, int odometer, CancellationToken cancellationToken)
    {
        var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);

        foreach (var item in draft.Items.Where(i => i.ReminderId is not null))
        {
            var reminder = await reminders.GetForHouseholdAsync(item.ReminderId!.Value, householdId, cancellationToken);
            if (reminder is null)
            {
                continue;
            }

            if (item.NextMileageInterval is not null || item.NextMonthInterval is not null)
            {
                reminder.UpdateIntervals(item.NextMileageInterval, item.NextMonthInterval);
            }

            reminder.CompleteAt(odometer, draft.Date);
        }
    }

    /// <summary>Removes receipts uploaded for an entry that was never saved.</summary>
    private async Task DiscardStoredFilesAsync(CancellationToken cancellationToken)
    {
        if (_draft is null)
        {
            return;
        }

        foreach (var receipt in _draft.Receipts)
        {
            await fileStore.DeleteAsync(receipt.StorageKey, cancellationToken);
        }
    }

    private ServiceDraft NewDraft(Guid vehicleId) => new()
    {
        VehicleId = vehicleId,
        Date = clock.Today
    };
}
