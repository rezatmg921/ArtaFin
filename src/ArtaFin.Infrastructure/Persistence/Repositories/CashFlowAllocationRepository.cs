using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Infrastructure.Persistence.Repositories;

internal sealed class CashFlowAllocationRepository(ArtaFinDbContext dbContext)
    : ICashFlowAllocationRepository
{
    public async Task LockSourceEntryAsync(
        Guid financialBookId,
        Guid journalEntryId,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Cash-flow allocation changes require an active transaction.");

        // All operations for a source entry serialize on its immutable header row.
        await dbContext.JournalEntries
            .FromSqlInterpolated($@"SELECT * FROM [JournalEntries] WITH (UPDLOCK, HOLDLOCK)
                WHERE [FinancialBookId] = {financialBookId} AND [Id] = {journalEntryId}")
            .AsNoTracking()
            .Select(entry => entry.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task LockAllocationSetAsync(
        Guid financialBookId,
        Guid allocationSetId,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Cash-flow allocation changes require an active transaction.");

        await dbContext.CashFlowAllocationSets
            .FromSqlInterpolated($@"SELECT * FROM [CashFlowAllocationSets] WITH (UPDLOCK, HOLDLOCK)
                WHERE [FinancialBookId] = {financialBookId} AND [Id] = {allocationSetId}")
            .AsNoTracking()
            .Select(set => set.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<CashFlowSourceLine?> GetSourceLineAsync(
        Guid financialBookId,
        Guid journalEntryId,
        int lineNumber,
        CancellationToken cancellationToken)
    {
        var query =
            from entry in dbContext.JournalEntries.AsNoTracking()
            where entry.FinancialBookId == financialBookId && entry.Id == journalEntryId
            from line in entry.Lines
            join account in dbContext.Accounts.AsNoTracking()
                on new { line.FinancialBookId, line.AccountId }
                equals new { account.FinancialBookId, AccountId = account.Id }
            where line.LineNumber == lineNumber
            select new CashFlowSourceLine(
                entry.Id,
                entry.FinancialBookId,
                line.LineNumber,
                account.Id,
                account.Type,
                account.CashFlowAccountType,
                line.Side,
                EF.Property<decimal>(line, "_amount"),
                EF.Property<string>(line, "_currency"));

        return query.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<CashFlowAllocationSet?> GetByIdAsync(
        Guid financialBookId,
        Guid allocationSetId,
        bool tracking,
        CancellationToken cancellationToken)
    {
        if (tracking)
        {
            // A tracking query does not refresh an entity already present in the
            // DbContext identity map. Allocation operations acquire the SQL row
            // lock before calling this method, so refresh any previously tracked
            // aggregate (including its shadow RowVersion) while that lock is held.
            var tracked = dbContext.CashFlowAllocationSets.Local
                .SingleOrDefault(set =>
                    set.FinancialBookId == financialBookId &&
                    set.Id == allocationSetId);

            if (tracked is not null)
            {
                await dbContext.Entry(tracked).ReloadAsync(cancellationToken);

                var allocations = dbContext.Entry(tracked)
                    .Collection(set => set.Allocations);
                allocations.IsLoaded = false;
                await allocations.LoadAsync(cancellationToken);

                return tracked;
            }
        }

        IQueryable<CashFlowAllocationSet> query = dbContext.CashFlowAllocationSets
            .Include(set => set.Allocations)
            .Where(set => set.FinancialBookId == financialBookId && set.Id == allocationSetId);

        if (!tracking)
            query = query.AsNoTracking();

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CashFlowAllocationSet>> GetBySourceAsync(
        Guid financialBookId,
        Guid journalEntryId,
        int lineNumber,
        CancellationToken cancellationToken) =>
        await dbContext.CashFlowAllocationSets
            .AsNoTracking()
            .Include(set => set.Allocations)
            .Where(set => set.FinancialBookId == financialBookId &&
                          set.SourceJournalEntryId == journalEntryId &&
                          set.SourceLineNumber == lineNumber)
            .OrderBy(set => set.Version)
            .ToListAsync(cancellationToken);

    public Task<CashFlowAllocationSet?> GetDraftForSourceAsync(
        Guid journalEntryId,
        int lineNumber,
        CancellationToken cancellationToken) =>
        dbContext.CashFlowAllocationSets
            .AsNoTracking()
            .Where(set => set.SourceJournalEntryId == journalEntryId &&
                          set.SourceLineNumber == lineNumber &&
                          set.Status == CashFlowAllocationSetStatus.Draft)
            .OrderByDescending(set => set.Version)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<CashFlowAllocationSet?> GetFinalizedForSourceAsync(
        Guid journalEntryId,
        int lineNumber,
        CancellationToken cancellationToken) =>
        dbContext.CashFlowAllocationSets
            .Include(set => set.Allocations)
            .Where(set => set.SourceJournalEntryId == journalEntryId &&
                          set.SourceLineNumber == lineNumber &&
                          set.Status == CashFlowAllocationSetStatus.Finalized)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<int> GetNextVersionAsync(
        Guid journalEntryId,
        int lineNumber,
        CancellationToken cancellationToken)
    {
        var latestVersion = await dbContext.CashFlowAllocationSets
            .Where(set => set.SourceJournalEntryId == journalEntryId &&
                          set.SourceLineNumber == lineNumber)
            .Select(set => (int?)set.Version)
            .MaxAsync(cancellationToken);

        return (latestVersion ?? 0) + 1;
    }

    public async Task<decimal> GetAllocatedAmountForCounterpartAsync(
        Guid journalEntryId,
        int sourceLineNumber,
        int counterpartLineNumber,
        CancellationToken cancellationToken)
    {
        // While a revision is Draft, the previous Finalized version remains
        // authoritative for other source lines. For the line being edited,
        // reserve the Draft's current allocations instead. This avoids both
        // double-counting one line's old and new versions and freeing capacity
        // before a revision is actually finalized.
        var sets = await dbContext.CashFlowAllocationSets
            .AsNoTracking()
            .Include(set => set.Allocations)
            .Where(set => set.SourceJournalEntryId == journalEntryId &&
                          set.Status != CashFlowAllocationSetStatus.Superseded)
            .ToListAsync(cancellationToken);

        var effectiveAllocations = sets
            .GroupBy(set => set.SourceLineNumber)
            .Select(group =>
            {
                var preferredStatus = group.Key == sourceLineNumber
                    ? CashFlowAllocationSetStatus.Draft
                    : CashFlowAllocationSetStatus.Finalized;

                return group
                    .Where(set => set.Status == preferredStatus)
                    .OrderByDescending(set => set.Version)
                    .FirstOrDefault()
                    ?? group.OrderByDescending(set => set.Version).First();
            })
            .SelectMany(set => set.Allocations);

        return effectiveAllocations
            .Where(allocation => allocation.CounterpartLineNumber == counterpartLineNumber)
            .Sum(allocation => allocation.Amount);
    }

    public async Task AddAsync(
        CashFlowAllocationSet allocationSet,
        CancellationToken cancellationToken)
    {
        await dbContext.CashFlowAllocationSets.AddAsync(allocationSet, cancellationToken);
    }

    public void DeleteDraft(CashFlowAllocationSet allocationSet)
    {
        dbContext.CashFlowAllocationSets.Remove(allocationSet);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
