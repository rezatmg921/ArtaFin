using ArtaFin.Domain.Finance;
using ArtaFin.Domain.Obligations;

namespace ArtaFin.Application.Features.Reports.GetBalanceSheet;

public sealed record BalanceSheetAccountResult(
    Guid AccountId,
    string Code,
    string Name,
    AccountType Type,
    decimal Amount,
    BalanceSheetClassification BalanceSheetClassification = BalanceSheetClassification.Unclassified);

public sealed record BalanceSheetObligationMaturityResult(
    Guid ControlAccountId,
    FinancialObligationDirection Direction,
    string Currency,
    decimal CurrentAmount,
    decimal NonCurrentAmount,
    decimal TotalOutstanding,
    decimal LedgerBalance,
    decimal ReconciliationDifference,
    bool IsReconciled);

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
    decimal Difference,
    IReadOnlyList<BalanceSheetObligationMaturityResult> ObligationMaturities,
    decimal CurrentAssets = 0m,
    decimal NonCurrentAssets = 0m,
    decimal UnclassifiedAssets = 0m,
    decimal CurrentLiabilities = 0m,
    decimal NonCurrentLiabilities = 0m,
    decimal UnclassifiedLiabilities = 0m);
