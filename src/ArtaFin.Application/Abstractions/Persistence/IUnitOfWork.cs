using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Abstractions.Persistence;

/// <summary>
/// Commits the changes tracked by the current persistence unit of work.
/// Posting operations execute validation and persistence in one transaction.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken) => operation(cancellationToken);

    Task EnsureCanPostAsync(
        Guid financialBookId,
        IReadOnlyCollection<JournalLine> lines,
        CancellationToken cancellationToken) => Task.CompletedTask;
}
