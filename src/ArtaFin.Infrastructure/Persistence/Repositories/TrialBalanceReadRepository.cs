using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Infrastructure.Persistence.Repositories;

/// <summary>
/// Builds an as-of trial balance from posted journal lines without persisting totals.
/// </summary>
internal sealed class TrialBalanceReadRepository(ArtaFinDbContext dbContext)
    : ITrialBalanceReadRepository
{
    public async Task<IReadOnlyList<TrialBalanceAccountSnapshot>> GetAsOfAsync(
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
                account.Type
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
                    line.Side == PostingSide.Debit ? line.Amount : 0m),
                Credits = group.Sum(line =>
                    line.Side == PostingSide.Credit ? line.Amount : 0m)
            })
            .ToDictionaryAsync(item => item.AccountId, cancellationToken);

        return accounts.Select(account =>
        {
            totals.TryGetValue(account.Id, out var total);
            return new TrialBalanceAccountSnapshot(
                account.Id,
                account.Code,
                account.Name,
                account.Type,
                total?.Debits ?? 0m,
                total?.Credits ?? 0m);
        }).ToArray();
    }
}
