using System.Text.Json;
using Garage.Application.Abstractions;
using Garage.Application.ServiceLogging;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace Garage.Web.Services;

/// <summary>
/// Story L-4: an in-progress service entry survives leaving the flow, and a restart.
/// Kept in protected local storage on the device — the draft is scratch work, not a
/// record, so it does not belong in the database until the user saves it.
/// </summary>
public class ServiceDraftStore(ProtectedLocalStorage storage) : IServiceDraftStore
{
    private const string Key = "garage.serviceDraft";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public async Task<ServiceDraft?> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await storage.GetAsync<string>(Key);
            if (!result.Success || string.IsNullOrWhiteSpace(result.Value))
            {
                return null;
            }

            return JsonSerializer.Deserialize<ServiceDraft>(result.Value, Options);
        }
        catch (Exception ex) when (ex is InvalidOperationException or JsonException)
        {
            // Unavailable during prerender, or written by an older shape of the draft.
            return null;
        }
    }

    public async Task SaveAsync(ServiceDraft draft, CancellationToken cancellationToken = default)
    {
        try
        {
            await storage.SetAsync(Key, JsonSerializer.Serialize(draft, Options));
        }
        catch (InvalidOperationException)
        {
            // Nothing to persist yet during prerender.
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await storage.DeleteAsync(Key);
        }
        catch (InvalidOperationException)
        {
        }
    }
}
