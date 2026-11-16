using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Infrastructure.Persistence.Repositories;

/// <summary>
/// Derives account balances from posted journal lines; no balance is persisted.
/// </summary>
internal sealed class AccountBalanceReadRepository(ArtaFinDbContext dbContext)
    : IAccountBalanceReadRepository
{
    public async Task<AccountBalancePage> GetPageAsync(
        Guid financialBookId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var accountsQuery = dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.FinancialBookId == financialBookId);

        var totalCount = await accountsQuery.CountAsync(cancellationToken);
        var accounts = await accountsQuery
            .OrderBy(account => account.Code)
            .ThenBy(account => account.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(account => new
            {
                account.Id,
                account.Code,
                account.Name,
                account.Type,
                account.NormalBalance,
                account.Status
            })
            .ToListAsync(cancellationToken);

        if (accounts.Count == 0)
            return new AccountBalancePage([], totalCount);

        var accountIds = accounts.Select(account => account.Id).ToArray();
        var lines = await dbContext.JournalEntries
            .AsNoTracking()
            .Where(entry => entry.FinancialBookId == financialBookId)
            .SelectMany(
                entry => entry.Lines,
                (entry, line) => new
                {
                    line.AccountId,
                    line.Side,
                    Amount = EF.Property<decimal>(line, "_amount")
                })
            .Where(line => accountIds.Contains(line.AccountId))
            .ToListAsync(cancellationToken);

        var totals = lines
            .GroupBy(line => line.AccountId)
            .ToDictionary(
                group => group.Key,
                group => new
                {
                    Debits = group.Where(line => line.Side == PostingSide.Debit)
                        .Sum(line => line.Amount),
                    Credits = group.Where(line => line.Side == PostingSide.Credit)
                        .Sum(line => line.Amount)
                });

        var items = accounts.Select(account =>
        {
            totals.TryGetValue(account.Id, out var total);
            var debits = total?.Debits ?? 0m;
            var credits = total?.Credits ?? 0m;
            var balance = account.NormalBalance == AccountNormalBalance.Debit
                ? debits - credits
                : credits - debits;

            return new AccountBalanceSnapshot(
                account.Id,
                account.Code,
                account.Name,
                account.Type,
                account.NormalBalance,
                account.Status,
                debits,
                credits,
                balance);
        }).ToArray();

        return new AccountBalancePage(items, totalCount);
    }
}
