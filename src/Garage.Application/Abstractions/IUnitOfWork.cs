namespace Garage.Application.Abstractions;

/// <summary>One commit boundary shared by every repository in a request.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
