using Garage.Domain;

namespace Garage.Web.Api.Contracts;

public record DocumentCardResponse(
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
    bool IsImage,
    string Url,
    bool IsExpiringSoon,
    bool HasExpired,
    bool NeedsAttention,
    string ExpiryDescription);

public record ReceiptGroupResponse(
    Guid ServiceRecordId,
    DateOnly Date,
    string Summary,
    IReadOnlyList<DocumentCardResponse> Receipts);

public record ExpiringDocumentResponse(DocumentCardResponse Document, string VehicleNickname);
