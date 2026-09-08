using Garage.Domain.Common;

namespace Garage.Domain.Entities;

/// <summary>
/// A file kept with a vehicle: insurance, registration, title, inspection, or a
/// receipt photographed while logging a service.
/// </summary>
public class Document : Entity
{
    private Document() { }

    public Document(Guid vehicleId, DocumentType type, string title, string fileName, string contentType, string storagePath, long sizeBytes)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("A document needs a title.");
        }

        if (string.IsNullOrWhiteSpace(storagePath))
        {
            throw new DomainException("A document needs a stored file.");
        }

        VehicleId = vehicleId;
        Type = type;
        Title = title.Trim();
        FileName = fileName;
        ContentType = contentType;
        StoragePath = storagePath;
        SizeBytes = sizeBytes;
    }

    public Guid VehicleId { get; private set; }
    public Vehicle? Vehicle { get; private set; }

    public DocumentType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public string StoragePath { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public DateOnly? ExpiresOn { get; private set; }

    /// <summary>Set on receipts, so a receipt can link back to the visit it came from (story D-3).</summary>
    public Guid? ServiceRecordId { get; private set; }
    public ServiceRecord? ServiceRecord { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; } = DateTimeOffset.UtcNow;

    public bool IsImage => ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    public void SetExpiry(DateOnly? expiresOn) => ExpiresOn = expiresOn;

    public void AttachToServiceRecord(Guid serviceRecordId)
    {
        ServiceRecordId = serviceRecordId;
        Type = DocumentType.Receipt;
    }

    public void Retitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("A document needs a title.");
        }

        Title = title.Trim();
    }

    /// <summary>Days until expiry; negative once it has lapsed, null when it never expires.</summary>
    public int? DaysUntilExpiry(DateOnly today) =>
        ExpiresOn is null ? null : ExpiresOn.Value.DayNumber - today.DayNumber;
}
