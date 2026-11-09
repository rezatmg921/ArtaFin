namespace ArtaFin.Application.Abstractions.Persistence;

/// <summary>
/// Commits the changes tracked by the current persistence unit of work.
/// The implementation is responsible for the underlying database transaction boundary.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
