namespace ArtaFin.Application.Abstractions.Persistence;

/// <summary>
/// Read-only persistence port for trial balance reporting.
/// </summary>
public interface ITrialBalanceReadRepository
{
    Task<IReadOnlyList<TrialBalanceAccountSnapshot>> GetAsOfAsync(
        Guid financialBookId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken);
}

public sealed record TrialBalanceAccountSnapshot(
    Guid AccountId,
    string Code,
    string Name,
    ArtaFin.Domain.Finance.AccountType Type,
    decimal TotalDebits,
    decimal TotalCredits);
