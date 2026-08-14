using Garage.Application.Abstractions;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace Garage.Web.Services;

/// <summary>
/// Story F-3: the chosen vehicle persists between sessions. Protected local storage
/// keeps it on the device and signed, so it survives a restart without becoming a
/// value the client can forge into another household's vehicle id — the id is still
/// checked against the household on every read.
/// </summary>
public class SelectedVehicleStore(ProtectedLocalStorage storage) : ISelectedVehicleStore
{
    private const string Key = "garage.selectedVehicle";

    public async Task<Guid?> GetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await storage.GetAsync<Guid>(Key);
            return result.Success ? result.Value : null;
        }
        catch (InvalidOperationException)
        {
            // Storage is unavailable during prerender; the caller falls back to the first vehicle.
            return null;
        }
    }

    public async Task SetAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        try
        {
            await storage.SetAsync(Key, vehicleId);
        }
        catch (InvalidOperationException)
        {
            // Same as above — nothing to persist yet.
        }
    }
}
