using Garage.Domain.Common;

namespace Garage.Application.Abstractions;

/// <summary>
/// The shape every aggregate repository shares. Aggregate-specific queries live on
/// the derived interfaces so the Application layer never has to hand a query
/// expression across the boundary.
/// </summary>
public interface IRepository<T> where T : Entity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    void Remove(T entity);
}
