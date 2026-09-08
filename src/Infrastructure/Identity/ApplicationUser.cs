using Microsoft.AspNetCore.Identity;

namespace Garage.Infrastructure.Identity;

/// <summary>
/// Identity is a persistence concern, so the user record lives in Infrastructure.
/// The Domain knows only about the household this user belongs to.
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>The household whose cars this user can see. Assigned on first sign-in.</summary>
    public Guid HouseholdId { get; set; }

    public string? DisplayName { get; set; }
}
