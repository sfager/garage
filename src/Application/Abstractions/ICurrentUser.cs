namespace Garage.Application.Abstractions;

/// <summary>
/// The signed-in user's household. Everything the Application layer reads or writes
/// is scoped through this, so one household can never see another's cars.
///
/// Every member is async: under Blazor Server the authentication state is only
/// available asynchronously, and blocking on it would deadlock the circuit.
/// </summary>
public interface ICurrentUser
{
    Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default);

    Task<string?> GetUserIdAsync(CancellationToken cancellationToken = default);

    Task<string?> GetDisplayNameAsync(CancellationToken cancellationToken = default);

    /// <summary>Throws when nobody is signed in; call only from authorized paths.</summary>
    Task<Guid> GetHouseholdIdAsync(CancellationToken cancellationToken = default);

    /// <summary>Null when nobody is signed in, for paths that render either way.</summary>
    Task<Guid?> TryGetHouseholdIdAsync(CancellationToken cancellationToken = default);
}
