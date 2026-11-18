using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Features.Reports.GetCashFlowStatement;

public sealed record CashFlowActivityResult(
    CashFlowActivity Activity,
    decimal CashReceipts,
    decimal CashPayments,
    decimal NetCashFlow);

public sealed record CashFlowCategoryResult(
    CashFlowActivity Activity,
    string Category,
    decimal CashReceipts,
    decimal CashPayments,
    decimal NetCashFlow);

public sealed record CashFlowStatementExceptionResult(
    string Code,
    string Message,
    Guid JournalEntryId,
    int SourceLineNumber,
    DateTimeOffset PostedAt,
    string Description,
    decimal Amount,
    string Currency);

public sealed record CashFlowNonCashDisclosureResult(
    Guid Id,
    Guid JournalEntryId,
    int SourceLineNumber,
    DateTimeOffset PostedAt,
    string Description,
    decimal Amount,
    string Currency,
    CashFlowActivity Activity,
    string Category,
    string? Memo);

public sealed record CashFlowStatementResult(
    DateTimeOffset From,
    DateTimeOffset To,
    string Currency,
    decimal OpeningCashAndCashEquivalents,
    IReadOnlyList<CashFlowActivityResult> Activities,
    IReadOnlyList<CashFlowCategoryResult> Categories,
    decimal NetIncreaseDecrease,
    decimal ClosingCashAndCashEquivalents,
    decimal ReconciliationDifference,
    bool IsReconciled,
    IReadOnlyList<CashFlowStatementExceptionResult> Exceptions,
    IReadOnlyList<CashFlowNonCashDisclosureResult> NonCashDisclosures);
