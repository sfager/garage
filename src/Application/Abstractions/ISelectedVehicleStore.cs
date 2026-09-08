namespace Garage.Application.Abstractions;

/// <summary>
/// Where the chosen vehicle is remembered between sessions (story F-3). The
/// Application layer only needs get and set; the Web layer decides it is a cookie.
/// </summary>
public interface ISelectedVehicleStore
{
    Task<Guid?> GetAsync(CancellationToken cancellationToken = default);
    Task SetAsync(Guid vehicleId, CancellationToken cancellationToken = default);
}
