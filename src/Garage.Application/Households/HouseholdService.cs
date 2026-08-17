using Garage.Application.Abstractions;
using Garage.Domain.Common;
using Garage.Domain.Entities;
using Garage.Domain.Repositories;

namespace Garage.Application.Households;

/// <summary>
/// Lets two people keep one garage. Accounts already exist per person; this is what
/// actually joins them to the same household so they see the same cars.
/// </summary>
public class HouseholdService(
    IHouseholdRepository households,
    IHouseholdInvitationRepository invitations,
    IVehicleRepository vehicles,
    IUserDirectory users,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<HouseholdOverview> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);
        var household = await households.GetByIdAsync(householdId, cancellationToken)
            ?? throw new DomainException("Your household could not be found.");

        var members = await users.ListMembersAsync(householdId, cancellationToken);
        var garage = await vehicles.ListAllAsync(householdId, cancellationToken);

        var issued = await invitations.ListForHouseholdAsync(householdId, cancellationToken);
        var now = clock.UtcNow;

        var summaries = issued
            .Select(i => new InvitationSummary(i.Id, i.CreatedUtc, i.ExpiresUtc, i.IsPending(now), Describe(i, now)))
            .ToList();

        // Leaving would strand the cars if nobody else could reach them.
        var canLeave = members.Count > 1 || garage.Count == 0;
        var reason = canLeave
            ? null
            : "You are the only person here. Invite someone else first, or the cars would be left unreachable.";

        return new HouseholdOverview(householdId, household.Name, members, summaries, garage.Count, canLeave, reason);
    }

    public async Task RenameAsync(string name, CancellationToken cancellationToken = default)
    {
        var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);
        var household = await households.GetByIdAsync(householdId, cancellationToken)
            ?? throw new DomainException("Your household could not be found.");

        household.Rename(name);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Creates an invitation and returns the code — the only time it exists in the clear.</summary>
    public async Task<CreatedInvitation> InviteAsync(CancellationToken cancellationToken = default)
    {
        var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);
        var userId = await currentUser.GetUserIdAsync(cancellationToken)
            ?? throw new DomainException("You need to be signed in to invite someone.");

        var (invitation, code) = HouseholdInvitation.Create(householdId, userId, clock.UtcNow);

        await invitations.AddAsync(invitation, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreatedInvitation(invitation.Id, code, invitation.ExpiresUtc);
    }

    public async Task RevokeAsync(Guid invitationId, CancellationToken cancellationToken = default)
    {
        var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);
        var invitation = await invitations.GetByIdAsync(invitationId, cancellationToken);

        // Scoped to the caller's household so an id alone cannot reach another's invitation.
        if (invitation is null || invitation.HouseholdId != householdId)
        {
            throw new DomainException("That invitation is not one of yours.");
        }

        invitation.Revoke(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// What accepting would do, shown before the decision. In particular it counts the
    /// cars the invited person would bring with them, because that is the surprising part.
    /// </summary>
    public async Task<InvitationPreview> PreviewAsync(string code, CancellationToken cancellationToken = default)
    {
        var invitation = await FindAsync(code, cancellationToken);
        var now = clock.UtcNow;

        if (invitation is null)
        {
            return new InvitationPreview(false, "That invitation code is not recognised.", "", "", 0, 0, false);
        }

        var problem = invitation.ProblemFor(now) switch
        {
            InvitationProblem.AlreadyUsed => "That invitation has already been used.",
            InvitationProblem.Withdrawn => "That invitation was withdrawn.",
            InvitationProblem.Expired => "That invitation has expired. Ask for a new one.",
            _ => null
        };

        var household = await households.GetByIdAsync(invitation.HouseholdId, cancellationToken);
        var theirGarage = await vehicles.ListAllAsync(invitation.HouseholdId, cancellationToken);
        var invitedBy = await users.FindAsync(invitation.CreatedByUserId, cancellationToken);

        var myHouseholdId = await currentUser.TryGetHouseholdIdAsync(cancellationToken);
        var alreadyAMember = myHouseholdId == invitation.HouseholdId;

        var myGarage = myHouseholdId is { } mine && !alreadyAMember
            ? await vehicles.ListAllAsync(mine, cancellationToken)
            : [];

        return new InvitationPreview(
            problem is null && !alreadyAMember,
            alreadyAMember ? "You are already part of this household." : problem,
            household?.Name ?? "a garage",
            invitedBy?.DisplayName ?? "someone",
            theirGarage.Count,
            myGarage.Count,
            alreadyAMember);
    }

    /// <summary>
    /// Joins the household. Any cars the joining person already had come with them, so
    /// nothing is stranded in a household nobody can reach any more; their old, now empty
    /// household is cleaned up.
    /// </summary>
    public async Task<Guid> AcceptAsync(string code, CancellationToken cancellationToken = default)
    {
        var invitation = await FindAsync(code, cancellationToken)
            ?? throw new DomainException("That invitation code is not recognised.");

        var userId = await currentUser.GetUserIdAsync(cancellationToken)
            ?? throw new DomainException("You need to be signed in to accept an invitation.");

        var previousHouseholdId = await currentUser.TryGetHouseholdIdAsync(cancellationToken);

        if (previousHouseholdId == invitation.HouseholdId)
        {
            throw new DomainException("You are already part of this household.");
        }

        // Throws with the reason when used, withdrawn or expired.
        invitation.Accept(userId, clock.UtcNow);

        if (previousHouseholdId is { } previous)
        {
            await MoveVehiclesAsync(previous, invitation.HouseholdId, cancellationToken);
        }

        await users.MoveToHouseholdAsync(userId, invitation.HouseholdId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (previousHouseholdId is { } old)
        {
            await RemoveIfDesertedAsync(old, cancellationToken);
        }

        return invitation.HouseholdId;
    }

    /// <summary>
    /// Leaves the shared household for a fresh, empty one of your own. The shared cars
    /// stay behind with whoever is left.
    /// </summary>
    public async Task LeaveAsync(CancellationToken cancellationToken = default)
    {
        var overview = await GetOverviewAsync(cancellationToken);

        if (!overview.CanLeave)
        {
            throw new DomainException(overview.CannotLeaveReason ?? "You cannot leave this household.");
        }

        var userId = await currentUser.GetUserIdAsync(cancellationToken)
            ?? throw new DomainException("You need to be signed in.");

        var member = await users.FindAsync(userId, cancellationToken);
        var household = new Household($"{member?.DisplayName ?? "My"} garage");

        await households.AddAsync(household, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await users.MoveToHouseholdAsync(userId, household.Id, cancellationToken);

        await RemoveIfDesertedAsync(overview.HouseholdId, cancellationToken);
    }

    private async Task MoveVehiclesAsync(Guid from, Guid to, CancellationToken cancellationToken)
    {
        var garage = await vehicles.ListAllAsync(from, cancellationToken);

        foreach (var vehicle in garage)
        {
            vehicle.MoveToHousehold(to);
        }
    }

    /// <summary>Tidies up a household nobody belongs to and nothing lives in.</summary>
    private async Task RemoveIfDesertedAsync(Guid householdId, CancellationToken cancellationToken)
    {
        var remaining = await users.CountMembersAsync(householdId, cancellationToken);
        if (remaining > 0)
        {
            return;
        }

        var garage = await vehicles.ListAllAsync(householdId, cancellationToken);
        if (garage.Count > 0)
        {
            return;
        }

        var household = await households.GetByIdAsync(householdId, cancellationToken);
        if (household is null)
        {
            return;
        }

        households.Remove(household);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private Task<HouseholdInvitation?> FindAsync(string code, CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(code)
            ? Task.FromResult<HouseholdInvitation?>(null)
            : invitations.GetByCodeHashAsync(HouseholdInvitation.HashCode(code), cancellationToken);

    private static string Describe(HouseholdInvitation invitation, DateTimeOffset now) => invitation switch
    {
        { AcceptedUtc: not null } => "accepted",
        { RevokedUtc: not null } => "withdrawn",
        _ when now >= invitation.ExpiresUtc => "expired",
        _ => "waiting"
    };
}
