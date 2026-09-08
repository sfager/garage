using Garage.Application.ServiceLogging;

namespace Garage.Application.Abstractions;

/// <summary>
/// Where an in-progress service entry is kept between steps and between sessions
/// (story L-4). The Application layer only needs load, save and clear.
/// </summary>
public interface IServiceDraftStore
{
    Task<ServiceDraft?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(ServiceDraft draft, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
