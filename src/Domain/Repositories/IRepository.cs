using Garage.Domain.Common;

namespace Garage.Domain.Repositories;

/// <summary>
/// The shape every aggregate repository shares. Aggregate-specific queries live on
/// the derived interfaces so no caller has to hand a query expression across the
/// boundary — the contract names what it can answer.
/// </summary>
public interface IRepository<T> where T : Entity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    void Remove(T entity);
}
