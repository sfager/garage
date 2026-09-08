using System.Net.Http.Json;
using Garage.Domain.Common;
using Garage.Web.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Garage.Web.Services.Api;

internal static class ApiClientErrors
{
    public static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
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
                throw new DomainException(message);
            }
        }
        catch (DomainException)
        {
            throw;
        }
        catch
        {
            // Continue to other parsing options.
        }

        try
        {
            var validation = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
            var first = validation?.Errors.Values.SelectMany(v => v).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first))
            {
                message = first;
            }
        }
        catch
        {
            // Keep generic message when validation payload is absent or malformed.
        }

        throw new DomainException(message);
    }
}
