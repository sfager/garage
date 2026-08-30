using System.Net.Http.Json;
using Garage.Application.Notifications;
using Garage.Web.Api.Contracts;

namespace Garage.Web.Services.Api;

public class NotificationApiClient(HttpClient http)
{
    public async Task<NotificationStatusResponse> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync("api/notifications/status", cancellationToken);
        await ApiClientErrors.EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<NotificationStatusResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Could not read notification status.");
    }

    public async Task SubscribeAsync(PushSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("api/notifications/subscribe", request, cancellationToken);
        await ApiClientErrors.EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task UnsubscribeAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("api/notifications/unsubscribe", new UnsubscribeRequest(endpoint), cancellationToken);
        await ApiClientErrors.EnsureSuccessAsync(response, cancellationToken);
    }
}
