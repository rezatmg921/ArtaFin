using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Abstractions.Persistence;

public interface ICashFlowAllocationRepository
{
    Task LockSourceEntryAsync(Guid financialBookId, Guid journalEntryId, CancellationToken cancellationToken);
    Task LockAllocationSetAsync(Guid financialBookId, Guid allocationSetId, CancellationToken cancellationToken);
    Task<CashFlowSourceLine?> GetSourceLineAsync(Guid financialBookId, Guid journalEntryId, int lineNumber, CancellationToken cancellationToken);
    Task<CashFlowAllocationSet?> GetByIdAsync(Guid financialBookId, Guid allocationSetId, bool tracking, CancellationToken cancellationToken);
    Task<IReadOnlyList<CashFlowAllocationSet>> GetBySourceAsync(Guid financialBookId, Guid journalEntryId, int lineNumber, CancellationToken cancellationToken);
    Task<CashFlowAllocationSet?> GetDraftForSourceAsync(Guid journalEntryId, int lineNumber, CancellationToken cancellationToken);
    Task<CashFlowAllocationSet?> GetFinalizedForSourceAsync(Guid journalEntryId, int lineNumber, CancellationToken cancellationToken);
    Task<int> GetNextVersionAsync(Guid journalEntryId, int lineNumber, CancellationToken cancellationToken);
    Task<decimal> GetAllocatedAmountForCounterpartAsync(Guid journalEntryId, int sourceLineNumber, int counterpartLineNumber, CancellationToken cancellationToken);
    Task AddAsync(CashFlowAllocationSet allocationSet, CancellationToken cancellationToken);
    void AddAllocation(CashFlowAllocation allocation);
    void DeleteDraft(CashFlowAllocationSet allocationSet);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record CashFlowSourceLine(
    Guid JournalEntryId,
    Guid FinancialBookId,
    int LineNumber,
    Guid AccountId,
    AccountType AccountType,
    CashFlowAccountType CashFlowAccountType,
    PostingSide Side,
    decimal Amount,
    string Currency);
