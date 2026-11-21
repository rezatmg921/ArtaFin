using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Features.Reports.GetBalanceSheet;

public sealed record BalanceSheetAccountResult(
    Guid AccountId,
    string Code,
    string Name,
    AccountType Type,
    decimal Amount);

public sealed record BalanceSheetResult(
    DateTimeOffset AsOf,
    string Currency,
    IReadOnlyList<BalanceSheetAccountResult> Assets,
    IReadOnlyList<BalanceSheetAccountResult> Liabilities,
    IReadOnlyList<BalanceSheetAccountResult> Equity,
    decimal UnclosedNetIncome,
    decimal TotalAssets,
    decimal TotalLiabilities,
    decimal TotalEquity,
    decimal TotalLiabilitiesAndEquity,
    decimal Difference);
