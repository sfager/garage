using System.Net.Http.Json;
using Garage.Application.Mileage;
using Garage.Domain.Services;

namespace Garage.Web.Services.Api;

public class MileageApiClient(HttpClient http)
{
    public async Task<MileageSummary> GetSummaryAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"api/vehicles/{vehicleId}/mileage/summary", cancellationToken);
        await ApiClientErrors.EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<MileageSummary>(cancellationToken)
            ?? throw new InvalidOperationException("Could not read mileage summary.");
    }

    public async Task<IReadOnlyList<MileageLogEntry>> GetLogAsync(
        Guid vehicleId,
        MileageEntryKind? filter = null,
        CancellationToken cancellationToken = default)
    {
        var url = filter is null
            ? $"api/vehicles/{vehicleId}/mileage/log"
            : $"api/vehicles/{vehicleId}/mileage/log?filter={filter}";

        using var response = await http.GetAsync(url, cancellationToken);
        await ApiClientErrors.EnsureSuccessAsync(response, cancellationToken);

        var entries = await response.Content.ReadFromJsonAsync<List<MileageLogEntry>>(cancellationToken);
        return entries ?? [];
    }

    public async Task RecordReadingAsync(Guid vehicleId, RecordReadingRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync($"api/vehicles/{vehicleId}/mileage/readings", request, cancellationToken);
        await ApiClientErrors.EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task RecordTripAsync(Guid vehicleId, RecordTripRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync($"api/vehicles/{vehicleId}/mileage/trips", request, cancellationToken);
        await ApiClientErrors.EnsureSuccessAsync(response, cancellationToken);
    }
}
