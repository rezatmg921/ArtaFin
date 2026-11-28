namespace ArtaFin.Application.Abstractions.Persistence;

/// <summary>
/// Read-only persistence port for trial balance reporting.
/// </summary>
public interface ITrialBalanceReadRepository
{
    /// <summary>
    /// Returns cumulative posted totals through and including the supplied instant.
    /// </summary>
    Task<IReadOnlyList<TrialBalanceAccountSnapshot>> GetAsOfAsync(
        Guid financialBookId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns cumulative posted totals strictly before the supplied instant.
    /// Useful for reports whose period end is exclusive.
    /// </summary>
    Task<IReadOnlyList<TrialBalanceAccountSnapshot>> GetBeforeAsync(
        Guid financialBookId,
        DateTimeOffset exclusiveEnd,
        CancellationToken cancellationToken);
}

public sealed record TrialBalanceAccountSnapshot(
    Guid AccountId,
    string Code,
    string Name,
    ArtaFin.Domain.Finance.AccountType Type,
    ArtaFin.Domain.Finance.AccountNormalBalance NormalBalance,
    decimal TotalDebits,
    decimal TotalCredits);
