using Garage.Application.Abstractions;
using Garage.Domain;
using Garage.Domain.Common;
using Garage.Domain.Entities;
using Garage.Domain.Repositories;

namespace Garage.Application.Documents;

/// <summary>Epic E6. Vehicle paperwork, its expiry warnings, and the receipts logging leaves behind.</summary>
public class DocumentService(
    IVehicleRepository vehicles,
    IDocumentRepository documents,
    IServiceRecordRepository serviceRecords,
    IReminderRepository reminders,
    IFileStore fileStore,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    /// <summary>How far ahead story D-2 counts as a warning.</summary>
    public const int ExpiryWarningDays = 30;

    /// <summary>
    /// Story D-1. Files the upload against the vehicle. The stream is written first so a
    /// failed database write cannot leave a document row pointing at nothing.
    /// </summary>
    public async Task<Document> UploadAsync(
        Guid vehicleId,
        DocumentUploadRequest request,
        Stream content,
        string fileName,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var vehicle = await RequireVehicleAsync(vehicleId, cancellationToken);
        var key = await fileStore.SaveAsync(content, fileName, "documents", cancellationToken);

        try
        {
            var document = new Document(vehicle.Id, request.Type, request.Title, fileName, contentType, key, sizeBytes);
            document.SetExpiry(request.ExpiresOn);

            vehicle.AddDocument(document);
            await documents.AddAsync(document, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return document;
        }
        catch
        {
            await fileStore.DeleteAsync(key, cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<DocumentCard>> ListFilesAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        await RequireVehicleAsync(vehicleId, cancellationToken);
        var files = await documents.ListFilesAsync(vehicleId, cancellationToken);

        // Anything needing attention floats to the top of the grid (story D-2).
        return [.. files
            .Select(ToCard)
            .OrderByDescending(d => d.NeedsAttention)
            .ThenBy(d => d.DaysUntilExpiry ?? int.MaxValue)
            .ThenBy(d => d.Title)];
    }

    /// <summary>Story D-3: receipts grouped by the service record they belong to.</summary>
    public async Task<IReadOnlyList<ReceiptGroup>> ListReceiptGroupsAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        await RequireVehicleAsync(vehicleId, cancellationToken);

        var receipts = await documents.ListReceiptsAsync(vehicleId, cancellationToken);
        if (receipts.Count == 0)
        {
            return [];
        }

        var records = await serviceRecords.ListForVehicleAsync(vehicleId, cancellationToken);
        var byId = records.ToDictionary(r => r.Id);

        return [.. receipts
            .Where(r => r.ServiceRecordId is not null && byId.ContainsKey(r.ServiceRecordId.Value))
            .GroupBy(r => r.ServiceRecordId!.Value)
            .Select(group =>
            {
                var record = byId[group.Key];
                return new ReceiptGroup(record.Id, record.Date, record.Summary, [.. group.Select(ToCard)]);
            })
            .OrderByDescending(g => g.Date)];
    }

    /// <summary>Story D-2: anything expiring within the window, across every vehicle.</summary>
    public async Task<IReadOnlyList<ExpiringDocument>> ListExpiringAsync(CancellationToken cancellationToken = default)
    {
        var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);
        var through = clock.Today.AddDays(ExpiryWarningDays);

        var expiring = await documents.ListExpiringAsync(householdId, through, cancellationToken);
        if (expiring.Count == 0)
        {
            return [];
        }

        var garage = await vehicles.ListAllAsync(householdId, cancellationToken);
        var names = garage.ToDictionary(v => v.Id, v => v.Nickname);

        return [.. expiring
            .Select(d => new ExpiringDocument(ToCard(d), names.GetValueOrDefault(d.VehicleId, "Vehicle")))
            .OrderBy(d => d.Document.DaysUntilExpiry ?? int.MaxValue)];
    }

    public async Task<DocumentCard?> GetAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);
        var document = await documents.GetForHouseholdAsync(documentId, householdId, cancellationToken);
        return document is null ? null : ToCard(document);
    }

    public async Task UpdateAsync(
        Guid documentId,
        string title,
        DocumentType type,
        DateOnly? expiresOn,
        CancellationToken cancellationToken = default)
    {
        var document = await RequireDocumentAsync(documentId, cancellationToken);

        document.Retitle(title);
        document.SetExpiry(expiresOn);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Story D-1: documents can be deleted, taking the stored file with them.</summary>
    public async Task DeleteAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await RequireDocumentAsync(documentId, cancellationToken);
        var key = document.StoragePath;

        documents.Remove(document);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await fileStore.DeleteAsync(key, cancellationToken);
    }

    /// <summary>
    /// Story D-2: a reminder set straight from the expiry warning. It fires a few days
    /// before the document lapses, which is the point of warning at all.
    /// </summary>
    public async Task<Reminder> CreateExpiryReminderAsync(
        Guid documentId,
        int daysBefore = 7,
        CancellationToken cancellationToken = default)
    {
        var document = await RequireDocumentAsync(documentId, cancellationToken);

        if (document.ExpiresOn is not { } expiry)
        {
            throw new DomainException("That document has no expiry date to remind you about.");
        }

        var vehicle = await RequireVehicleAsync(document.VehicleId, cancellationToken);

        // Never schedule the reminder in the past — an expiry inside the notice period
        // is due right away.
        var dueOn = expiry.AddDays(-daysBefore);
        if (dueOn < clock.Today)
        {
            dueOn = clock.Today;
        }

        var reminder = Reminder.OnDate(
            vehicle.Id,
            $"{document.Title} expires",
            dueOn,
            vehicle.CurrentOdometer,
            clock.Today);

        vehicle.AddReminder(reminder);
        await reminders.AddAsync(reminder, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return reminder;
    }

    public string GetUrl(string storagePath) => fileStore.GetUrl(storagePath);

    private DocumentCard ToCard(Document document) => new(
        document.Id,
        document.VehicleId,
        document.Type,
        document.Title,
        document.FileName,
        document.ContentType,
        document.StoragePath,
        document.SizeBytes,
        document.ExpiresOn,
        document.DaysUntilExpiry(clock.Today),
        document.IsImage);

    private async Task<Vehicle> RequireVehicleAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);
        return await vehicles.GetForHouseholdAsync(vehicleId, householdId, cancellationToken)
            ?? throw new DomainException("That vehicle is not in your garage.");
    }

    private async Task<Document> RequireDocumentAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);
        return await documents.GetForHouseholdAsync(documentId, householdId, cancellationToken)
            ?? throw new DomainException("That document is not in your garage.");
    }
}
