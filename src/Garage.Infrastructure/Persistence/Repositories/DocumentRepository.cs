using Garage.Application.Abstractions;
using Garage.Domain;
using Garage.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Garage.Infrastructure.Persistence.Repositories;

public class DocumentRepository(GarageDbContext context)
    : RepositoryBase<Document>(context), IDocumentRepository
{
    public async Task<IReadOnlyList<Document>> ListFilesAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        await Set.Where(d => d.VehicleId == vehicleId && d.Type != DocumentType.Receipt)
            .OrderBy(d => d.Title)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Document>> ListReceiptsAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        await Set.Where(d => d.VehicleId == vehicleId && d.Type == DocumentType.Receipt && d.ServiceRecordId != null)
            .OrderByDescending(d => d.CreatedUtc)
            .ToListAsync(cancellationToken);

    public Task<Document?> GetForHouseholdAsync(Guid documentId, Guid householdId, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(
            d => d.Id == documentId && d.Vehicle!.HouseholdId == householdId,
            cancellationToken);

    /// <summary>
    /// Expired documents are included as well as expiring ones — a registration that
    /// lapsed last week is more urgent than one lapsing next week, not less.
    /// </summary>
    public async Task<IReadOnlyList<Document>> ListExpiringAsync(
        Guid householdId,
        DateOnly through,
        CancellationToken cancellationToken = default) =>
        await Set.Where(d => d.Vehicle!.HouseholdId == householdId
                             && d.Type != DocumentType.Receipt
                             && d.ExpiresOn != null
                             && d.ExpiresOn <= through)
            .OrderBy(d => d.ExpiresOn)
            .ToListAsync(cancellationToken);
}
