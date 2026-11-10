using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Abstractions.Persistence;

/// <summary>
/// Persistence port for the Account aggregate root.
/// Implementations should return a tracked aggregate for command-side updates.
/// </summary>
public interface IAccountRepository
{
    /// <summary>
    /// Loads an account within its owning book boundary, preventing unscoped
    /// account lookups from leaking data across financial books.
    /// </summary>
    Task<Account?> GetByIdAsync(
        Guid financialBookId,
        Guid accountId,
        CancellationToken cancellationToken);

    /// <summary>Loads an account and its database-generated concurrency version.</summary>
    Task<AccountReadSnapshot?> GetReadSnapshotByIdAsync(
        Guid financialBookId,
        Guid accountId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Loads a tracked account snapshot for an update and optionally applies
    /// the client's expected version as EF Core's original concurrency value.
    /// </summary>
    Task<AccountReadSnapshot?> GetForUpdateAsync(
        Guid financialBookId,
        Guid accountId,
        byte[]? expectedRowVersion,
        CancellationToken cancellationToken);

    /// <summary>
    /// Performs an application-level uniqueness pre-check.
    /// A database unique constraint on (FinancialBookId, normalized Code)
    /// remains mandatory to protect against concurrent requests.
    /// </summary>
    Task<bool> ExistsByCodeAsync(
        Guid financialBookId,
        string normalizedCode,
        CancellationToken cancellationToken);

    Task AddAsync(
        Account account,
        CancellationToken cancellationToken);
}
