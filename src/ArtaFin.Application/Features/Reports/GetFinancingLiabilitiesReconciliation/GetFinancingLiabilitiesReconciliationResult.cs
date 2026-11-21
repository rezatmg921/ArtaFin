using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Features.Reports.GetFinancingLiabilitiesReconciliation;

public sealed record FinancingLiabilityMovementResult(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    FinancingLiabilityType Classification,
    decimal OpeningBalance,
    decimal CashChanges,
    decimal NonCashChanges,
    decimal UnclassifiedChanges,
    decimal ClosingBalance,
    decimal ReconciliationDifference);

public sealed record FinancingLiabilityMovementExceptionResult(
    string Code,
    string Message,
    Guid JournalEntryId,
    int SourceLineNumber,
    DateTimeOffset PostedAt,
    string Description,
    Guid AccountId,
    string AccountCode,
    string AccountName,
    decimal Amount,
    string Currency);

public sealed record FinancingLiabilitiesReconciliationResult(
    DateTimeOffset From,
    DateTimeOffset To,
    string Currency,
    IReadOnlyList<FinancingLiabilityMovementResult> Accounts,
    decimal TotalOpeningBalance,
    decimal TotalCashChanges,
    decimal TotalNonCashChanges,
    decimal TotalUnclassifiedChanges,
    decimal TotalClosingBalance,
    decimal TotalReconciliationDifference,
    bool IsComplete,
    IReadOnlyList<FinancingLiabilityMovementExceptionResult> Exceptions);
