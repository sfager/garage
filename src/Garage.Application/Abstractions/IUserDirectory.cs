namespace Garage.Application.Abstractions;

/// <summary>One person who can see a household's cars.</summary>
public record HouseholdMember(string UserId, string DisplayName, Guid HouseholdId);

/// <summary>
/// Reads and moves the people attached to a household. Identity is a persistence concern,
/// so the Application layer states what it needs and Infrastructure supplies it.
/// </summary>
public interface IUserDirectory
{
    Task<IReadOnlyList<HouseholdMember>> ListMembersAsync(Guid householdId, CancellationToken cancellationToken = default);

    Task<int> CountMembersAsync(Guid householdId, CancellationToken cancellationToken = default);

    Task<HouseholdMember?> FindAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Moves a person into another household — this is what joining actually is.</summary>
    Task MoveToHouseholdAsync(string userId, Guid householdId, CancellationToken cancellationToken = default);
}
