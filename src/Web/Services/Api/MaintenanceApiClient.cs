using System.Net;
using System.Net.Http.Json;
using Garage.Application.Maintenance;
using Garage.Domain.Common;
using Garage.Web.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Garage.Web.Services.Api;

public class MaintenanceApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<ReminderCard>> ListUpcomingAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var cards = await http.GetFromJsonAsync<List<ReminderCard>>($"api/vehicles/{vehicleId}/maintenance/upcoming", cancellationToken);
        return cards ?? [];
    }

    public async Task<IReadOnlyList<ReminderCard>> ListAllAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var cards = await http.GetFromJsonAsync<List<ReminderCard>>($"api/vehicles/{vehicleId}/maintenance/reminders", cancellationToken);
        return cards ?? [];
    }

    public async Task<ReminderPreview> PreviewAsync(Guid vehicleId, ReminderRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync($"api/vehicles/{vehicleId}/maintenance/reminders/preview", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<ReminderPreview>(cancellationToken)
            ?? throw new DomainException("Could not read reminder preview.");
    }

    public async Task SaveAsync(Guid vehicleId, ReminderRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync($"api/vehicles/{vehicleId}/maintenance/reminders", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task SnoozeAsync(Guid reminderId, SnoozeRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync($"api/maintenance/reminders/{reminderId}/snooze", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task DismissAsync(Guid reminderId, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsync($"api/maintenance/reminders/{reminderId}/dismiss", null, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task ReinstateAsync(Guid reminderId, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsync($"api/maintenance/reminders/{reminderId}/reinstate", null, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task SetNotificationsAsync(Guid reminderId, bool enabled, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync(
            $"api/maintenance/reminders/{reminderId}/notifications",
            new SetReminderNotificationsRequest(enabled),
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<ServiceHistoryEntry>> ListHistoryAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var history = await http.GetFromJsonAsync<List<ServiceHistoryEntry>>($"api/vehicles/{vehicleId}/maintenance/history", cancellationToken);
        return history ?? [];
    }

    public async Task<ServiceRecordDetailResponse?> GetServiceRecordAsync(Guid recordId, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"api/maintenance/history/{recordId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ServiceRecordDetailResponse>(cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var message = $"Request failed with status {(int)response.StatusCode}.";
        try
        {
            var payload = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken);
            if (!string.IsNullOrWhiteSpace(payload?.Message))
            {
                message = payload.Message;
            }
            else
            {
                var validation = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
                var first = validation?.Errors.Values.SelectMany(v => v).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(first))
                {
                    message = first;
                }
            }
        }
        catch
        {
            // Keep a generic message when no structured error payload is available.
        }

        throw new DomainException(message);
    }
}
