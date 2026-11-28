using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Infrastructure.Persistence.Repositories;

internal sealed class EquityMovementCorrectionRepository(ArtaFinDbContext dbContext)
    : IEquityMovementCorrectionRepository
{
    public async Task<IReadOnlyList<EquityMovementCorrection>> GetByJournalEntryAsync(
        Guid financialBookId,
        Guid journalEntryId,
        CancellationToken cancellationToken) =>
        await dbContext.EquityMovementCorrections
            .AsNoTracking()
            .Where(item => item.FinancialBookId == financialBookId &&
                           item.JournalEntryId == journalEntryId)
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

    public Task<JournalEntry?> GetReversalAsync(
        Guid financialBookId,
        Guid originalJournalEntryId,
        CancellationToken cancellationToken) =>
        dbContext.JournalEntries
            .AsNoTracking()
            .SingleOrDefaultAsync(entry =>
                entry.FinancialBookId == financialBookId &&
                entry.ReversesJournalEntryId == originalJournalEntryId,
                cancellationToken);

    public Task<bool> IsEquityAccountAsync(
        Guid financialBookId,
        Guid accountId,
        CancellationToken cancellationToken) =>
        dbContext.Accounts
            .AsNoTracking()
            .AnyAsync(account =>
                account.FinancialBookId == financialBookId &&
                account.Id == accountId &&
                account.Type == AccountType.Equity,
                cancellationToken);

    public async Task AddRangeAsync(
        IReadOnlyCollection<EquityMovementCorrection> corrections,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(corrections);
        await dbContext.EquityMovementCorrections.AddRangeAsync(corrections, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
