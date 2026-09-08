using System.Security.Claims;
using Garage.Application.Abstractions;
using Garage.Domain.Entities;
using Garage.Domain.Repositories;
using Garage.Infrastructure.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Garage.Web.Services;

/// <summary>
/// Resolves the signed-in user's household. A user who has never had one gets a
/// household created on first access, so registering is all it takes to start
/// adding cars; inviting a second person later means pointing them at the same id.
/// </summary>
public class CurrentUser(
    AuthenticationStateProvider authenticationStateProvider,
    IHttpContextAccessor httpContextAccessor,
    UserManager<ApplicationUser> userManager,
    IHouseholdRepository households,
    IUnitOfWork unitOfWork) : ICurrentUser
{
    private ClaimsPrincipal? _principal;
    private Guid? _householdId;

    public async Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default) =>
        (await GetPrincipalAsync()).Identity?.IsAuthenticated == true;

    public async Task<string?> GetUserIdAsync(CancellationToken cancellationToken = default) =>
        (await GetPrincipalAsync()).FindFirstValue(ClaimTypes.NameIdentifier);

    public async Task<string?> GetDisplayNameAsync(CancellationToken cancellationToken = default) =>
        (await GetPrincipalAsync()).Identity?.Name;

    public async Task<Guid> GetHouseholdIdAsync(CancellationToken cancellationToken = default) =>
        await TryGetHouseholdIdAsync(cancellationToken)
        ?? throw new InvalidOperationException("No signed-in user.");

    public async Task<Guid?> TryGetHouseholdIdAsync(CancellationToken cancellationToken = default)
    {
        if (_householdId is { } cached)
        {
            return cached;
        }

        var principal = await GetPrincipalAsync();
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return null;
        }

        if (user.HouseholdId == Guid.Empty)
        {
            var household = new Household($"{user.DisplayName ?? user.UserName ?? "My"} garage");
            await households.AddAsync(household, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            user.HouseholdId = household.Id;
            await userManager.UpdateAsync(user);
        }

        _householdId = user.HouseholdId;
        return user.HouseholdId;
    }

    private async Task<ClaimsPrincipal> GetPrincipalAsync()
    {
        if (_principal is not null)
        {
            return _principal;
        }

        var requestUser = httpContextAccessor.HttpContext?.User;
        if (requestUser?.Identity?.IsAuthenticated == true)
        {
            _principal = requestUser;
            return _principal;
        }

        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        _principal = state.User;
        return _principal;
    }
}
