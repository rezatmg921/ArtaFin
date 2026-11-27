using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Abstractions.Persistence;

/// <summary>
/// Read-only source data for the statement of changes in equity.
/// </summary>
public interface IChangesInEquityReadRepository
{
    Task<ChangesInEquitySnapshot> GetPeriodAsync(
        Guid financialBookId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);
}

public sealed record ChangesInEquityAccountSnapshot(
    Guid AccountId,
    string Code,
    string Name,
    AccountType Type,
    decimal OpeningDebits,
    decimal OpeningCredits,
    decimal PeriodDebits,
    decimal PeriodCredits,
    decimal ClosingDebits,
    decimal ClosingCredits);

public sealed record ChangesInEquitySnapshot(
    IReadOnlyList<ChangesInEquityAccountSnapshot> Accounts,
    decimal OpeningUnclosedNetIncome,
    decimal ClosingUnclosedNetIncome);
