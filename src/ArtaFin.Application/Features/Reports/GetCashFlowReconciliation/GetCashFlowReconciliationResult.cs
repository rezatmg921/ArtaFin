namespace ArtaFin.Application.Features.Reports.GetCashFlowReconciliation;

public sealed record CashFlowReconciliationResult(
    DateTimeOffset From,
    DateTimeOffset To,
    DateTimeOffset TrialBalanceAsOf,
    string Currency,
    decimal CashAccountDebits,
    decimal CashAccountCredits,
    decimal InternalTransfers,
    decimal ExternalCashInflowsFromTrialBalance,
    decimal ExternalCashOutflowsFromTrialBalance,
    decimal CashFlowReceipts,
    decimal CashFlowPayments,
    decimal ReceiptsDifference,
    decimal PaymentsDifference,
    bool IsReconciled,
    IReadOnlyList<CashFlowReconciliationAccountResult> Accounts);

public sealed record CashFlowReconciliationAccountResult(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    decimal Debits,
    decimal Credits);
