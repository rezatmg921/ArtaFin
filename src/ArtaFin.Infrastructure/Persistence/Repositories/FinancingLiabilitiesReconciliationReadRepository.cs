using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Infrastructure.Persistence.Repositories;

internal sealed class FinancingLiabilitiesReconciliationReadRepository(ArtaFinDbContext dbContext)
    : IFinancingLiabilitiesReconciliationReadRepository
{
    public async Task<FinancingLiabilitiesReconciliationReadSnapshot> GetBeforeAsync(
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
            where account.FinancingLiabilityType != FinancingLiabilityType.None
            select entry.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        var lines = await (
            from entry in dbContext.JournalEntries.AsNoTracking()
            where entry.FinancialBookId == financialBookId &&
                  entry.PostedAt < to &&
                  entryIds.Contains(entry.Id)
            from line in entry.Lines
            join account in dbContext.Accounts.AsNoTracking()
                on new { line.FinancialBookId, line.AccountId }
                equals new { account.FinancialBookId, AccountId = account.Id }
            select new FinancingLiabilityLineSnapshot(
                entry.Id,
                entry.PostedAt,
                entry.Description,
                line.LineNumber,
                account.Id,
                account.Code,
                account.Name,
                account.FinancingLiabilityType,
                account.CashFlowAccountType,
                line.Side,
                EF.Property<decimal>(line, "_amount"),
                EF.Property<string>(line, "_currency")))
            .ToListAsync(cancellationToken);

        var sets = await dbContext.CashFlowAllocationSets
            .AsNoTracking()
            .Include(set => set.Allocations)
            .Where(set => set.FinancialBookId == financialBookId &&
                          entryIds.Contains(set.SourceJournalEntryId) &&
                          set.Status == CashFlowAllocationSetStatus.Finalized)
            .ToListAsync(cancellationToken);

        var allocations = sets
            .SelectMany(set => set.Allocations.Select(allocation =>
                new FinancingLiabilityCashAllocationSnapshot(
                    set.SourceJournalEntryId,
                    set.SourceLineNumber,
                    allocation.CounterpartLineNumber!.Value,
                    allocation.Amount)))
            .ToArray();

        var disclosures = await (
            from disclosure in dbContext.CashFlowNonCashDisclosures.AsNoTracking()
            join entry in dbContext.JournalEntries.AsNoTracking()
                on disclosure.JournalEntryId equals entry.Id
            where disclosure.FinancialBookId == financialBookId &&
                  entry.FinancialBookId == financialBookId &&
                  entryIds.Contains(entry.Id) &&
                  entry.PostedAt < to
            select new FinancingLiabilityNonCashDisclosureSnapshot(
                entry.Id,
                disclosure.SourceLineNumber,
                disclosure.Amount,
                disclosure.Currency,
                disclosure.Category))
            .ToListAsync(cancellationToken);

        return new FinancingLiabilitiesReconciliationReadSnapshot(lines, allocations, disclosures);
    }
}
