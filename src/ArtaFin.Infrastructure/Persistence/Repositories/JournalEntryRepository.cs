using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Infrastructure.Persistence.Repositories;

internal sealed class JournalEntryRepository(ArtaFinDbContext dbContext)
    : IJournalEntryRepository
{
    public Task<JournalEntry?> GetByIdAsync(
        Guid financialBookId,
        Guid journalEntryId,
        CancellationToken cancellationToken)
    {
        return dbContext.JournalEntries
            .AsNoTracking()
            .SingleOrDefaultAsync(
                entry => entry.FinancialBookId == financialBookId &&
                         entry.Id == journalEntryId,
                cancellationToken);
    }

    public Task<bool> HasReversalAsync(
        Guid financialBookId,
        Guid originalJournalEntryId,
        CancellationToken cancellationToken)
    {
        return dbContext.JournalEntries
            .AsNoTracking()
            .AnyAsync(
                entry => entry.FinancialBookId == financialBookId &&
                         entry.ReversesJournalEntryId == originalJournalEntryId,
                cancellationToken);
    }

    public async Task AddAsync(JournalEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await dbContext.JournalEntries.AddAsync(entry, cancellationToken);
    }
}
