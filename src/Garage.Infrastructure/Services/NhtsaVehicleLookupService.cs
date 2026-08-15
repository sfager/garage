using System.Text.Json;
using System.Text.Json.Serialization;
using Garage.Application.Abstractions;
using Garage.Application.Vehicles;
using Microsoft.Extensions.Logging;

namespace Garage.Infrastructure.Services;

/// <summary>
/// Story V-1, VIN half. Decodes a VIN through the NHTSA vPIC service, which is public,
/// free and needs no key, but only knows vehicles sold in the United States — which
/// suits the miles-and-gallons decision.
///
/// Plate lookup has no free equivalent: turning a plate into a VIN needs a commercial
/// data provider. Until one is configured, a plate lookup reports that and the caller
/// falls back to manual entry, which V-1 requires for any failed lookup anyway.
/// </summary>
public class NhtsaVehicleLookupService(HttpClient http, ILogger<NhtsaVehicleLookupService> logger)
    : IVehicleLookupService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<VehicleLookupResult> LookupAsync(
        LookupMethod method,
        string identifier,
        CancellationToken cancellationToken = default)
    {
        identifier = identifier?.Trim() ?? string.Empty;

        if (method == LookupMethod.Plate)
        {
            return VehicleLookupResult.Failed(
                "Plate lookup is not connected to a data provider yet. Enter the details below.");
        }

        if (identifier.Length != 17)
        {
            return VehicleLookupResult.Failed("A VIN is 17 characters. Check it, or enter the details below.");
        }

        try
        {
            var response = await http.GetAsync(
                $"vehicles/DecodeVinValues/{Uri.EscapeDataString(identifier)}?format=json",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("vPIC returned {StatusCode} for a VIN lookup", response.StatusCode);
                return VehicleLookupResult.Failed("The lookup service is not responding. Enter the details below.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<VpicResponse>(stream, JsonOptions, cancellationToken);
            var result = payload?.Results?.FirstOrDefault();

            if (result is null || string.IsNullOrWhiteSpace(result.Make))
            {
                return VehicleLookupResult.Failed("We could not identify that VIN. Enter the details below.");
            }

            return new VehicleLookupResult
            {
                Found = true,
                Vin = identifier.ToUpperInvariant(),
                Year = int.TryParse(result.ModelYear, out var year) ? year : null,
                Make = TitleCase(result.Make),
                Model = TitleCase(result.Model),
                Trim = CleanTrim(result.Trim ?? result.Series),
                Engine = DescribeEngine(result)
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // A lookup failure is an expected path, not a fault: V-1 wants manual entry.
            logger.LogWarning(ex, "VIN lookup failed");
            return VehicleLookupResult.Failed("The lookup service could not be reached. Enter the details below.");
        }
    }

    /// <summary>Builds "2.5L H4" from the pieces vPIC returns, falling back as data thins out.</summary>
    private static string? DescribeEngine(VpicResult result)
    {
        var displacement = decimal.TryParse(result.DisplacementL, out var litres)
            ? $"{litres:0.0}L"
            : null;

        var cylinders = int.TryParse(result.EngineCylinders, out var count) ? count : (int?)null;

        var configuration = result.EngineConfiguration?.ToLowerInvariant() ?? string.Empty;
        var layout = configuration switch
        {
            var c when c.Contains("horizontal") => "H",
            var c when c.Contains("v-shaped") || c.Contains("v shaped") => "V",
            var c when c.Contains("in-line") || c.Contains("inline") => "I",
            _ => null
        };

        var engine = (layout, cylinders) switch
        {
            (not null, not null) => $"{layout}{cylinders}",
            (null, not null) => $"{cylinders}-cyl",
            _ => null
        };

        return string.Join(' ', new[] { displacement, engine }.Where(p => p is not null)) is { Length: > 0 } combined
            ? combined
            : null;
    }

    /// <summary>vPIC returns trims like "Limited+M/R+ES+NAVI"; the plus signs are separators.</summary>
    private static string? CleanTrim(string? trim)
    {
        if (string.IsNullOrWhiteSpace(trim))
        {
            return null;
        }

        return trim.Replace('+', ' ').Trim() is { Length: > 0 } cleaned ? cleaned : null;
    }

    /// <summary>vPIC shouts makes ("SUBARU"); the wireframes show them cased normally.</summary>
    private static string? TitleCase(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Any(char.IsLower)
            ? trimmed
            : string.Join(' ', trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => word.Length == 1
                    ? word
                    : char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()));
    }

    private sealed class VpicResponse
    {
        [JsonPropertyName("Results")] public List<VpicResult>? Results { get; set; }
    }

    private sealed class VpicResult
    {
        public string? ModelYear { get; set; }
        public string? Make { get; set; }
        public string? Model { get; set; }
        public string? Trim { get; set; }
        public string? Series { get; set; }
        public string? DisplacementL { get; set; }
        public string? EngineCylinders { get; set; }
        public string? EngineConfiguration { get; set; }
    }
}
