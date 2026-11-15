using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Infrastructure.Persistence.Repositories;

internal sealed class JournalEntryRepository(ArtaFinDbContext dbContext)
    : IJournalEntryRepository, IJournalEntryReadRepository
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

    public async Task<JournalEntryPage> GetPageAsync(
        Guid financialBookId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? searchTerm,
        bool? isReversal,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.JournalEntries
            .AsNoTracking()
            .Where(entry => entry.FinancialBookId == financialBookId);

        if (from.HasValue)
            query = query.Where(entry => entry.PostedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(entry => entry.PostedAt <= to.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
            query = query.Where(entry => entry.Description.Contains(searchTerm));

        if (isReversal.HasValue)
        {
            query = isReversal.Value
                ? query.Where(entry => entry.ReversesJournalEntryId != null)
                : query.Where(entry => entry.ReversesJournalEntryId == null);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(entry => entry.PostedAt)
            .ThenByDescending(entry => entry.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(entry => new JournalEntryListItem(
                entry.Id,
                entry.PostedAt,
                entry.Description,
                entry.ReversesJournalEntryId,
                entry.Lines.Count))
            .ToListAsync(cancellationToken);

        return new JournalEntryPage(items, totalCount);
    }
}
