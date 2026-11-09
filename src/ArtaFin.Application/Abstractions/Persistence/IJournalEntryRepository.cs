using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Abstractions.Persistence;

/// <summary>Persistence port for posted journal entries.</summary>
public interface IJournalEntryRepository
{
    Task<JournalEntry?> GetByIdAsync(
        Guid financialBookId,
        Guid journalEntryId,
        CancellationToken cancellationToken);

    Task AddAsync(JournalEntry entry, CancellationToken cancellationToken);
}
