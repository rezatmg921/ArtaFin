using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Abstractions.Persistence;

public interface ICashFlowNonCashDisclosureRepository
{
    Task<CashFlowSourceLine?> GetSourceLineAsync(
        Guid financialBookId,
        Guid journalEntryId,
        int lineNumber,
        CancellationToken cancellationToken);

    Task AddAsync(CashFlowNonCashDisclosure disclosure, CancellationToken cancellationToken);

    Task<IReadOnlyList<CashFlowNonCashDisclosure>> GetByJournalEntryAsync(
        Guid financialBookId,
        Guid journalEntryId,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
