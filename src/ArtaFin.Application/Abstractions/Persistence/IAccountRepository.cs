using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Abstractions.Persistence;

/// <summary>
/// Persistence port for the Account aggregate root.
/// Implementations should return a tracked aggregate for command-side updates.
/// </summary>
public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid financialBookId, Guid accountId, CancellationToken cancellationToken);

    Task<AccountReadSnapshot?> GetReadSnapshotByIdAsync(Guid financialBookId, Guid accountId, CancellationToken cancellationToken);

    Task<AccountReadSnapshot?> GetForUpdateAsync(
        Guid financialBookId,
        Guid accountId,
        byte[]? expectedRowVersion,
        CancellationToken cancellationToken);

    Task<bool> ExistsByCodeAsync(Guid financialBookId, string normalizedCode, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the account only when no journal line references it.
    /// Returns false when accounting history exists.
    /// </summary>
    Task<bool> DeleteIfUnusedAsync(Account account, CancellationToken cancellationToken);

    Task AddAsync(Account account, CancellationToken cancellationToken);
}