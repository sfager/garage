using System.ComponentModel.DataAnnotations;
using Garage.Domain;

namespace Garage.Application.Documents;

/// <summary>Story D-1: type, optional expiry, and the file itself [1l].</summary>
public class DocumentUploadRequest
{
    [Required(ErrorMessage = "Give the document a title.")]
    [StringLength(160)]
    public string Title { get; set; } = string.Empty;

    public DocumentType Type { get; set; } = DocumentType.Insurance;

    public DateOnly? ExpiresOn { get; set; }
}

/// <summary>One tile in the documents grid.</summary>
public record DocumentCard(
    Guid Id,
    Guid VehicleId,
    DocumentType Type,
    string Title,
    string FileName,
    string ContentType,
    string StoragePath,
    long SizeBytes,
    DateOnly? ExpiresOn,
    int? DaysUntilExpiry,
    bool IsImage)
{
    /// <summary>Story D-2's threshold: inside 30 days is a warning, past it is expired.</summary>
    public bool IsExpiringSoon => DaysUntilExpiry is >= 0 and <= 30;

    public bool HasExpired => DaysUntilExpiry is < 0;

    public bool NeedsAttention => IsExpiringSoon || HasExpired;

    public string ExpiryDescription => DaysUntilExpiry switch
    {
        null => "no expiry",
        < 0 and var days => $"expired {Math.Abs(days)} day{(days == -1 ? "" : "s")} ago",
        0 => "expires today",
        var days => $"expires in {days} day{(days == 1 ? "" : "s")}"
    };
}

/// <summary>Story D-3: receipts grouped by the visit they came from [1l].</summary>
public record ReceiptGroup(
    Guid ServiceRecordId,
    DateOnly Date,
    string Summary,
    IReadOnlyList<DocumentCard> Receipts);

/// <summary>Story D-2, on Home: a warning that names the car as well as the document.</summary>
public record ExpiringDocument(DocumentCard Document, string VehicleNickname);
