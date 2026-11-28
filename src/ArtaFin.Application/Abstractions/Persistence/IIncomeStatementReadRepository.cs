using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Abstractions.Persistence;

/// <summary>
/// Read-only persistence port for income statement activity within a period.
/// </summary>
public interface IIncomeStatementReadRepository
{
    Task<IReadOnlyList<IncomeStatementAccountSnapshot>> GetPeriodAsync(
        Guid financialBookId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);
}

public sealed record IncomeStatementAccountSnapshot(
    Guid AccountId,
    string Code,
    string Name,
    AccountType Type,
    decimal TotalDebits,
    decimal TotalCredits);
