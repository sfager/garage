using Garage.Application.Abstractions;
using Garage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Garage.Infrastructure.Identity;

/// <summary>
/// The people side of a household. Identity lives here, so the move that joining a
/// household actually performs — repointing a user's HouseholdId — happens here too.
/// </summary>
public class UserDirectory(GarageDbContext context) : IUserDirectory
{
    public async Task<IReadOnlyList<HouseholdMember>> ListMembersAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        var users = await context.Users
            .Where(u => u.HouseholdId == householdId)
            .OrderBy(u => u.Email)
            .Select(u => new { u.Id, u.DisplayName, u.Email, u.UserName, u.HouseholdId })
            .ToListAsync(cancellationToken);

        return [.. users.Select(u => new HouseholdMember(
            u.Id,
            u.DisplayName ?? u.Email ?? u.UserName ?? "Someone",
            u.HouseholdId))];
    }

    public Task<int> CountMembersAsync(Guid householdId, CancellationToken cancellationToken = default) =>
        context.Users.CountAsync(u => u.HouseholdId == householdId, cancellationToken);

    public async Task<HouseholdMember?> FindAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await context.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.Id, u.DisplayName, u.Email, u.UserName, u.HouseholdId })
            .FirstOrDefaultAsync(cancellationToken);

        return user is null
            ? null
            : new HouseholdMember(user.Id, user.DisplayName ?? user.Email ?? user.UserName ?? "Someone", user.HouseholdId);
    }

    public async Task MoveToHouseholdAsync(string userId, Guid householdId, CancellationToken cancellationToken = default)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException($"User {userId} no longer exists.");

        user.HouseholdId = householdId;
        await context.SaveChangesAsync(cancellationToken);
    }
}
