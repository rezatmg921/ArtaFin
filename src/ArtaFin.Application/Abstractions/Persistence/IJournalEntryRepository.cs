using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Abstractions.Persistence;

/// <summary>Persistence port for posted journal entries.</summary>
public interface IJournalEntryRepository
{
    Task<JournalEntry?> GetByIdAsync(
        Guid financialBookId,
        Guid journalEntryId,
        CancellationToken cancellationToken);

    Task<JournalEntry?> GetByIdempotencyKeyAsync(
        Guid financialBookId,
        Guid userId,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        Task.FromResult<JournalEntry?>(null);

    Task<bool> HasReversalAsync(
        Guid financialBookId,
        Guid originalJournalEntryId,
        CancellationToken cancellationToken);

    Task<bool> IsLinkedToObligationPaymentAsync(
        Guid financialBookId,
        Guid journalEntryId,
        CancellationToken cancellationToken) =>
        Task.FromResult(false);

    Task AddAsync(JournalEntry entry, CancellationToken cancellationToken);
}
