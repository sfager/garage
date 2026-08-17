using Garage.Domain.Entities;
using Garage.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Garage.Infrastructure.Persistence.Repositories;

public class HouseholdInvitationRepository(GarageDbContext context)
    : RepositoryBase<HouseholdInvitation>(context), IHouseholdInvitationRepository
{
    public Task<HouseholdInvitation?> GetByCodeHashAsync(string codeHash, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(i => i.CodeHash == codeHash, cancellationToken);

    public async Task<IReadOnlyList<HouseholdInvitation>> ListForHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default) =>
        await Set.Where(i => i.HouseholdId == householdId)
            .OrderByDescending(i => i.CreatedUtc)
            .ToListAsync(cancellationToken);
}
