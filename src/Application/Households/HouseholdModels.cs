using Garage.Application.Abstractions;

namespace Garage.Application.Households;

/// <summary>An invitation as its household sees it. The code is not here — it is gone.</summary>
public record InvitationSummary(
    Guid Id,
    DateTimeOffset CreatedUtc,
    DateTimeOffset ExpiresUtc,
    bool IsPending,
    string Status);

/// <summary>The one and only time the code is available: right after it is created.</summary>
public record CreatedInvitation(Guid Id, string Code, DateTimeOffset ExpiresUtc);

/// <summary>
/// What the invited person is told before they decide. It names the household and says
/// plainly what accepting will do to the cars they already have.
/// </summary>
public record InvitationPreview(
    bool IsUsable,
    string? Problem,
    string HouseholdName,
    string InvitedBy,
    int VehiclesInHousehold,
    int VehiclesYouWouldBring,
    bool AlreadyAMember);

/// <summary>The household settings screen.</summary>
public record HouseholdOverview(
    Guid HouseholdId,
    string Name,
    IReadOnlyList<HouseholdMember> Members,
    IReadOnlyList<InvitationSummary> Invitations,
    int VehicleCount,
    bool CanLeave,
    string? CannotLeaveReason);
