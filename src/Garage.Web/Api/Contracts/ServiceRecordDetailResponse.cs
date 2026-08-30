using Garage.Domain;

namespace Garage.Web.Api.Contracts;

public record ServiceRecordDetailResponse(
    Guid Id,
    DateOnly Date,
    int Odometer,
    string Summary,
    ServiceCategory Category,
    decimal TotalCost,
    decimal? PartsCost,
    decimal? LaborCost,
    string? Shop,
    string? Notes,
    IReadOnlyList<ServiceRecordItemResponse> Items,
    IReadOnlyList<ServiceReceiptResponse> Receipts);

public record ServiceRecordItemResponse(string Name);

public record ServiceReceiptResponse(string Title, string StoragePath, bool IsImage, string Url);
