using System.Net.Http.Json;
using Garage.Application.Fuel;

namespace Garage.Web.Services.Api;

public class FuelApiClient(HttpClient http)
{
    public async Task<FuelScreen> GetScreenAsync(
        Guid vehicleId,
        FuelRange range = FuelRange.SixMonths,
        FuelMetric metric = FuelMetric.Mpg,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/vehicles/{vehicleId}/fuel/screen?range={range}&metric={metric}";
        using var response = await http.GetAsync(url, cancellationToken);
        await ApiClientErrors.EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<FuelScreen>(cancellationToken)
            ?? throw new InvalidOperationException("Could not read fuel screen.");
    }

    public async Task SaveAsync(Guid vehicleId, FuelEntryRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync($"api/vehicles/{vehicleId}/fuel/entries", request, cancellationToken);
        await ApiClientErrors.EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task DeleteAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        using var response = await http.DeleteAsync($"api/fuel/entries/{entryId}", cancellationToken);
        await ApiClientErrors.EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListStationsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync("api/fuel/stations", cancellationToken);
        await ApiClientErrors.EnsureSuccessAsync(response, cancellationToken);

        var stations = await response.Content.ReadFromJsonAsync<List<string>>(cancellationToken);
        return stations ?? [];
    }
}
