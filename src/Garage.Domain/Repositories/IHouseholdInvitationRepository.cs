using Garage.Domain.Entities;

namespace Garage.Domain.Repositories;

public interface IHouseholdInvitationRepository : IRepository<HouseholdInvitation>
{
    /// <summary>
    /// Looks an invitation up by the hash of its code. The raw code is never stored, so
    /// this is the only way in.
    /// </summary>
    Task<HouseholdInvitation?> GetByCodeHashAsync(string codeHash, CancellationToken cancellationToken = default);

    /// <summary>Invitations a household has issued, newest first, for its settings screen.</summary>
    Task<IReadOnlyList<HouseholdInvitation>> ListForHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default);
}
