using System.Net.Http.Json;
using Garage.Application.Households;
using Garage.Domain.Common;
using Garage.Web.Api.Contracts;

namespace Garage.Web.Services.Api;

public class HouseholdApiClient(HttpClient http)
{
    public async Task<HouseholdOverview> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync("api/households/overview", cancellationToken);
        await ApiClientErrors.EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<HouseholdOverview>(cancellationToken)
            ?? throw new DomainException("Could not read household overview.");
    }

    public async Task<CreatedInvitation> InviteAsync(CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsync("api/households/invite", null, cancellationToken);
        await ApiClientErrors.EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<CreatedInvitation>(cancellationToken)
            ?? throw new DomainException("Could not read created invitation.");
    }

    public async Task RevokeAsync(Guid invitationId, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsync($"api/households/invitations/{invitationId}/revoke", null, cancellationToken);
        await ApiClientErrors.EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<InvitationPreview> PreviewAsync(string code, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("api/households/preview", new InvitationCodeRequest(code), cancellationToken);
        await ApiClientErrors.EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<InvitationPreview>(cancellationToken)
            ?? throw new DomainException("Could not read invitation preview.");
    }

    public async Task AcceptAsync(string code, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("api/households/accept", new InvitationCodeRequest(code), cancellationToken);
        await ApiClientErrors.EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task LeaveAsync(CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsync("api/households/leave", null, cancellationToken);
        await ApiClientErrors.EnsureSuccessAsync(response, cancellationToken);
    }
}
