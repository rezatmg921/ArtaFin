using ArtaFin.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Infrastructure.Persistence.Repositories;

/// <summary>
/// Reads all account debit and credit totals through the requested date.
/// Calculated balances are not persisted.
/// </summary>
internal sealed class BalanceSheetReadRepository(ArtaFinDbContext dbContext)
    : IBalanceSheetReadRepository
{
    public async Task<IReadOnlyList<BalanceSheetAccountSnapshot>> GetAsOfAsync(
        Guid financialBookId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken)
    {
        var accounts = await dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.FinancialBookId == financialBookId)
            .OrderBy(account => account.Code)
            .ThenBy(account => account.Id)
            .Select(account => new
            {
                account.Id,
                account.Code,
                account.Name,
                account.Type,
                account.BalanceSheetClassification
            })
            .ToListAsync(cancellationToken);

        if (accounts.Count == 0)
            return [];

        var totals = await dbContext.JournalEntries
            .AsNoTracking()
            .Where(entry =>
                entry.FinancialBookId == financialBookId &&
                entry.PostedAt <= asOf)
            .SelectMany(
                entry => entry.Lines,
                (entry, line) => new
                {
                    line.AccountId,
                    line.Side,
                    Amount = EF.Property<decimal>(line, "_amount")
                })
            .GroupBy(line => line.AccountId)
            .Select(group => new
            {
                AccountId = group.Key,
                Debits = group.Sum(line =>
                    line.Side == ArtaFin.Domain.Finance.PostingSide.Debit ? line.Amount : 0m),
                Credits = group.Sum(line =>
                    line.Side == ArtaFin.Domain.Finance.PostingSide.Credit ? line.Amount : 0m)
            })
            .ToDictionaryAsync(item => item.AccountId, cancellationToken);

        return accounts.Select(account =>
        {
            totals.TryGetValue(account.Id, out var total);
            return new BalanceSheetAccountSnapshot(
                account.Id,
                account.Code,
                account.Name,
                account.Type,
                total?.Debits ?? 0m,
                total?.Credits ?? 0m,
                account.BalanceSheetClassification);
        }).ToArray();
    }
}
