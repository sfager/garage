using System.Net;
using System.Net.Http.Json;
using Garage.Application.Vehicles;
using Garage.Domain.Common;
using Garage.Domain.Repositories;
using Garage.Web.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Garage.Web.Services.Api;

public class VehicleApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<VehicleSummary>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var vehicles = await http.GetFromJsonAsync<List<VehicleSummary>>("api/vehicles", cancellationToken);
        return vehicles ?? [];
    }

    public async Task<VehicleDetailResponse?> GetAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"api/vehicles/{vehicleId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<VehicleDetailResponse>(cancellationToken);
    }

    public async Task<VehicleSummary> AddAsync(AddVehicleRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("api/vehicles", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<VehicleSummary>(cancellationToken)
            ?? throw new DomainException("Could not read the added vehicle.");
    }

    public async Task UpdateAsync(EditVehicleRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PutAsJsonAsync($"api/vehicles/{request.Id}", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task ArchiveAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsync($"api/vehicles/{vehicleId}/archive", null, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task RestoreAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsync($"api/vehicles/{vehicleId}/restore", null, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<VehicleDeletionImpact?> GetDeletionImpactAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"api/vehicles/{vehicleId}/deletion-impact", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<VehicleDeletionImpact>(cancellationToken);
    }

    public async Task DeleteAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        using var response = await http.DeleteAsync($"api/vehicles/{vehicleId}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task SetPhotoAsync(Guid vehicleId, Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        using var multipart = new MultipartFormDataContent();
        using var streamContent = new StreamContent(content);
        multipart.Add(streamContent, "file", fileName);

        using var response = await http.PostAsync($"api/vehicles/{vehicleId}/photo", multipart, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task RemovePhotoAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        using var response = await http.DeleteAsync($"api/vehicles/{vehicleId}/photo", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
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
            // Keep the generic status message when the payload is absent or malformed.
        }

        throw new DomainException(message);
    }
}
