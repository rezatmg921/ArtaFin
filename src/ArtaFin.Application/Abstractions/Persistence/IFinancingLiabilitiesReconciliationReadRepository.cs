using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Abstractions.Persistence;

public interface IFinancingLiabilitiesReconciliationReadRepository
{
    Task<FinancingLiabilitiesReconciliationReadSnapshot> GetBeforeAsync(
        Guid financialBookId,
        DateTimeOffset to,
        CancellationToken cancellationToken);
}

public sealed record FinancingLiabilitiesReconciliationReadSnapshot(
    IReadOnlyList<FinancingLiabilityLineSnapshot> Lines,
    IReadOnlyList<FinancingLiabilityCashAllocationSnapshot> FinalizedAllocations,
    IReadOnlyList<FinancingLiabilityNonCashDisclosureSnapshot> NonCashDisclosures);

public sealed record FinancingLiabilityLineSnapshot(
    Guid JournalEntryId,
    DateTimeOffset PostedAt,
    string Description,
    int LineNumber,
    Guid AccountId,
    string AccountCode,
    string AccountName,
    FinancingLiabilityType FinancingLiabilityType,
    CashFlowAccountType CashFlowAccountType,
    PostingSide Side,
    decimal Amount,
    string Currency);

public sealed record FinancingLiabilityCashAllocationSnapshot(
    Guid JournalEntryId,
    int SourceLineNumber,
    int CounterpartLineNumber,
    decimal Amount);

public sealed record FinancingLiabilityNonCashDisclosureSnapshot(
    Guid JournalEntryId,
    int SourceLineNumber,
    decimal Amount,
    string Currency,
    string Category);
