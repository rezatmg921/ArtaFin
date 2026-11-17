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

    public Task<CashFlowAllocationSet?> GetByIdAsync(
        Guid financialBookId,
        Guid allocationSetId,
        bool tracking,
        CancellationToken cancellationToken)
    {
        IQueryable<CashFlowAllocationSet> query = dbContext.CashFlowAllocationSets
            .Include(set => set.Allocations)
            .Where(set => set.FinancialBookId == financialBookId && set.Id == allocationSetId);

        if (!tracking)
            query = query.AsNoTracking();

        return query.SingleOrDefaultAsync(cancellationToken);
    }

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

    public async Task AddAsync(
        CashFlowAllocationSet allocationSet,
        CancellationToken cancellationToken) =>
        await dbContext.CashFlowAllocationSets.AddAsync(allocationSet, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
