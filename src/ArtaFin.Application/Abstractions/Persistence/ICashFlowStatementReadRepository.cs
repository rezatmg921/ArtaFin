using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Abstractions.Persistence;

/// <summary>
/// Read-only source data for the IAS 7 direct-method cash-flow statement.
/// </summary>
public interface ICashFlowStatementReadRepository
{
    Task<CashFlowStatementReadSnapshot> GetBeforeAsync(
        Guid financialBookId,
        DateTimeOffset to,
        CancellationToken cancellationToken);
}

public sealed record CashFlowStatementReadSnapshot(
    IReadOnlyList<CashFlowStatementLineSnapshot> Lines,
    IReadOnlyList<CashFlowStatementAllocationSnapshot> FinalizedAllocations,
    IReadOnlyList<CashFlowStatementDraftSnapshot> Drafts);

public sealed record CashFlowStatementLineSnapshot(
    Guid JournalEntryId,
    DateTimeOffset PostedAt,
    string Description,
    int LineNumber,
    Guid AccountId,
    string AccountCode,
    string AccountName,
    AccountType AccountType,
    CashFlowAccountType CashFlowAccountType,
    PostingSide Side,
    decimal Amount,
    string Currency);

public sealed record CashFlowStatementAllocationSnapshot(
    Guid JournalEntryId,
    int SourceLineNumber,
    decimal Amount,
    CashFlowActivity Activity,
    string Category,
    int? CounterpartLineNumber);

public sealed record CashFlowStatementDraftSnapshot(
    Guid JournalEntryId,
    int SourceLineNumber);
