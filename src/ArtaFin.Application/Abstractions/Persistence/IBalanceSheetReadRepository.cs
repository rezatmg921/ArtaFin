namespace ArtaFin.Application.Abstractions.Persistence;

/// <summary>
/// Read-only persistence port for balance sheet reporting.
/// </summary>
public interface IBalanceSheetReadRepository
{
    Task<IReadOnlyList<BalanceSheetAccountSnapshot>> GetAsOfAsync(
        Guid financialBookId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken);
}

public sealed record BalanceSheetAccountSnapshot(
    Guid AccountId,
    string Code,
    string Name,
    ArtaFin.Domain.Finance.AccountType Type,
    decimal TotalDebits,
    decimal TotalCredits,
    ArtaFin.Domain.Finance.BalanceSheetClassification BalanceSheetClassification =
        ArtaFin.Domain.Finance.BalanceSheetClassification.Unclassified);
