using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Infrastructure.Persistence.Repositories;

/// <summary>
/// Reads posted journal lines, including counterpart accounts, and current
/// cash-flow allocation versions for IAS 7 direct-method reporting.
/// </summary>
internal sealed class CashFlowStatementReadRepository(ArtaFinDbContext dbContext)
    : ICashFlowStatementReadRepository
{
    public async Task<CashFlowStatementReadSnapshot> GetBeforeAsync(
        Guid financialBookId,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var entryIds = await (
            from entry in dbContext.JournalEntries.AsNoTracking()
            where entry.FinancialBookId == financialBookId && entry.PostedAt < to
            from line in entry.Lines
            join account in dbContext.Accounts.AsNoTracking()
                on new { line.FinancialBookId, line.AccountId }
                equals new { account.FinancialBookId, AccountId = account.Id }
            where account.CashFlowAccountType != CashFlowAccountType.None
            select entry.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (entryIds.Count == 0)
            return new CashFlowStatementReadSnapshot([], [], []);

        // Include every counterpart line from entries that contain cash so the
        // application layer can verify that finalized allocations reference
        // opposite-side, non-cash accounts.
        var lines = await (
            from entry in dbContext.JournalEntries.AsNoTracking()
            where entry.FinancialBookId == financialBookId &&
                  entry.PostedAt < to &&
                  entryIds.Contains(entry.Id)
            from line in entry.Lines
            join account in dbContext.Accounts.AsNoTracking()
                on new { line.FinancialBookId, line.AccountId }
                equals new { account.FinancialBookId, AccountId = account.Id }
            select new CashFlowStatementLineSnapshot(
                entry.Id,
                entry.PostedAt,
                entry.Description,
                line.LineNumber,
                account.Id,
                account.Code,
                account.Name,
                account.Type,
                account.CashFlowAccountType,
                line.Side,
                EF.Property<decimal>(line, "_amount"),
                EF.Property<string>(line, "_currency")))
            .ToListAsync(cancellationToken);

        var sets = await dbContext.CashFlowAllocationSets
            .AsNoTracking()
            .Include(set => set.Allocations)
            .Where(set =>
                set.FinancialBookId == financialBookId &&
                entryIds.Contains(set.SourceJournalEntryId) &&
                set.Status != CashFlowAllocationSetStatus.Superseded)
            .ToListAsync(cancellationToken);

        var finalized = sets
            .Where(set => set.Status == CashFlowAllocationSetStatus.Finalized)
            .SelectMany(set => set.Allocations.Select(allocation =>
                new CashFlowStatementAllocationSnapshot(
                    set.SourceJournalEntryId,
                    set.SourceLineNumber,
                    allocation.Amount,
                    allocation.Activity,
                    allocation.Category,
                    allocation.CounterpartLineNumber)))
            .ToArray();

        var drafts = sets
            .Where(set => set.Status == CashFlowAllocationSetStatus.Draft)
            .Select(set => new CashFlowStatementDraftSnapshot(
                set.SourceJournalEntryId, set.SourceLineNumber))
            .ToArray();

        return new CashFlowStatementReadSnapshot(lines, finalized, drafts);
    }
}
