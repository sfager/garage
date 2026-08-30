using System.Net.Http.Json;
using Garage.Application.Reporting;
using Garage.Web.Api.Contracts;

namespace Garage.Web.Services.Api;

public class ReportApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<ReportVehicleOption>> ListVehiclesAsync(CancellationToken cancellationToken = default)
    {
        var vehicles = await http.GetFromJsonAsync<List<ReportVehicleOption>>("api/reports/vehicles", cancellationToken);
        return vehicles ?? [];
    }

    public async Task<ReportScreen> GetScreenAsync(ReportFilter filter, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("api/reports/screen", filter, cancellationToken);
        await ApiClientErrors.EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<ReportScreen>(cancellationToken)
            ?? throw new InvalidOperationException("Could not read reports screen.");
    }

    public async Task<CsvExport> ExportAsync(ReportFilter filter, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("api/reports/export", filter, cancellationToken);
        await ApiClientErrors.EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<CsvExport>(cancellationToken)
            ?? throw new InvalidOperationException("Could not read reports export.");
    }
}
