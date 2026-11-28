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
    public Task<IReadOnlyList<TrialBalanceAccountSnapshot>> GetAsOfAsync(
        Guid financialBookId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken) =>
        GetThroughAsync(financialBookId, asOf, inclusive: true, cancellationToken);

    public Task<IReadOnlyList<TrialBalanceAccountSnapshot>> GetBeforeAsync(
        Guid financialBookId,
        DateTimeOffset exclusiveEnd,
        CancellationToken cancellationToken) =>
        GetThroughAsync(financialBookId, exclusiveEnd, inclusive: false, cancellationToken);

    private async Task<IReadOnlyList<TrialBalanceAccountSnapshot>> GetThroughAsync(
        Guid financialBookId,
        DateTimeOffset asOf,
        bool inclusive,
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
                account.NormalBalance
            })
            .ToListAsync(cancellationToken);

        if (accounts.Count == 0)
            return [];

        var totals = await dbContext.JournalEntries
            .AsNoTracking()
            .Where(entry =>
                entry.FinancialBookId == financialBookId &&
                (inclusive ? entry.PostedAt <= asOf : entry.PostedAt < asOf))
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
                account.NormalBalance,
                total?.Debits ?? 0m,
                total?.Credits ?? 0m);
        }).ToArray();
    }
}
