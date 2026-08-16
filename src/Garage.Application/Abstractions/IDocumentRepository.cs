using Garage.Domain;
using Garage.Domain.Entities;

namespace Garage.Application.Abstractions;

public interface IDocumentRepository : IRepository<Document>
{
    /// <summary>Story D-1: the vehicle's filed paperwork, receipts excluded.</summary>
    Task<IReadOnlyList<Document>> ListFilesAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Story D-3: receipts, with the service record each belongs to.</summary>
    Task<IReadOnlyList<Document>> ListReceiptsAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    Task<Document?> GetForHouseholdAsync(Guid documentId, Guid householdId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Story D-2: documents across the whole household expiring on or before a date,
    /// so Home can warn about any car's paperwork.
    /// </summary>
    Task<IReadOnlyList<Document>> ListExpiringAsync(Guid householdId, DateOnly through, CancellationToken cancellationToken = default);
}
