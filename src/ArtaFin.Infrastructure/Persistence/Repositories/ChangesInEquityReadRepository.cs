using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Infrastructure.Persistence.Repositories;

/// <summary>
/// Reads opening, period, and closing movements from posted journal lines only.
/// </summary>
internal sealed class ChangesInEquityReadRepository(ArtaFinDbContext dbContext)
    : IChangesInEquityReadRepository
{
    public async Task<ChangesInEquitySnapshot> GetPeriodAsync(
        Guid financialBookId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var accounts = await dbContext.Accounts
            .AsNoTracking()
            .Where(account =>
                account.FinancialBookId == financialBookId &&
                (account.Type == AccountType.Equity ||
                 account.Type == AccountType.Income ||
                 account.Type == AccountType.Expense))
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
            return new ChangesInEquitySnapshot([], 0m, 0m);

        var totals = await dbContext.JournalEntries
            .AsNoTracking()
            .Where(entry =>
                entry.FinancialBookId == financialBookId &&
                entry.PostedAt < to)
            .SelectMany(
                entry => entry.Lines,
                (entry, line) => new
                {
                    entry.PostedAt,
                    line.AccountId,
                    line.Side,
                    line.EquityMovementType,
                    Amount = EF.Property<decimal>(line, "_amount")
                })
            .GroupBy(line => line.AccountId)
            .Select(group => new
            {
                AccountId = group.Key,
                OpeningDebits = group.Sum(line =>
                    line.PostedAt < from && line.Side == PostingSide.Debit ? line.Amount : 0m),
                OpeningCredits = group.Sum(line =>
                    line.PostedAt < from && line.Side == PostingSide.Credit ? line.Amount : 0m),
                PeriodDebits = group.Sum(line =>
                    line.PostedAt >= from && line.PostedAt < to && line.Side == PostingSide.Debit ? line.Amount : 0m),
                PeriodCredits = group.Sum(line =>
                    line.PostedAt >= from && line.PostedAt < to && line.Side == PostingSide.Credit ? line.Amount : 0m),
                ClosingDebits = group.Sum(line =>
                    line.Side == PostingSide.Debit ? line.Amount : 0m),
                ClosingCredits = group.Sum(line =>
                    line.Side == PostingSide.Credit ? line.Amount : 0m),
                OwnerContributionCredits = group.Sum(line =>
                    line.PostedAt >= from && line.PostedAt < to &&
                    line.EquityMovementType == EquityMovementType.OwnerContribution &&
                    line.Side == PostingSide.Credit ? line.Amount : 0m),
                OwnerContributionDebits = group.Sum(line =>
                    line.PostedAt >= from && line.PostedAt < to &&
                    line.EquityMovementType == EquityMovementType.OwnerContribution &&
                    line.Side == PostingSide.Debit ? line.Amount : 0m),
                OwnerDistributionDebits = group.Sum(line =>
                    line.PostedAt >= from && line.PostedAt < to &&
                    line.EquityMovementType == EquityMovementType.OwnerDistribution &&
                    line.Side == PostingSide.Debit ? line.Amount : 0m),
                OwnerDistributionCredits = group.Sum(line =>
                    line.PostedAt >= from && line.PostedAt < to &&
                    line.EquityMovementType == EquityMovementType.OwnerDistribution &&
                    line.Side == PostingSide.Credit ? line.Amount : 0m),
                TransfersIn = group.Sum(line =>
                    line.PostedAt >= from && line.PostedAt < to &&
                    line.EquityMovementType == EquityMovementType.TransferBetweenEquityAccounts &&
                    line.Side == PostingSide.Credit ? line.Amount : 0m),
                TransfersOut = group.Sum(line =>
                    line.PostedAt >= from && line.PostedAt < to &&
                    line.EquityMovementType == EquityMovementType.TransferBetweenEquityAccounts &&
                    line.Side == PostingSide.Debit ? line.Amount : 0m),
                OtherIncreases = group.Sum(line =>
                    line.PostedAt >= from && line.PostedAt < to &&
                    line.EquityMovementType == EquityMovementType.Other &&
                    line.Side == PostingSide.Credit ? line.Amount : 0m),
                OtherDecreases = group.Sum(line =>
                    line.PostedAt >= from && line.PostedAt < to &&
                    line.EquityMovementType == EquityMovementType.Other &&
                    line.Side == PostingSide.Debit ? line.Amount : 0m),
                UnclassifiedIncreases = group.Sum(line =>
                    line.PostedAt >= from && line.PostedAt < to &&
                    line.EquityMovementType == EquityMovementType.Unclassified &&
                    line.Side == PostingSide.Credit ? line.Amount : 0m),
                UnclassifiedDecreases = group.Sum(line =>
                    line.PostedAt >= from && line.PostedAt < to &&
                    line.EquityMovementType == EquityMovementType.Unclassified &&
                    line.Side == PostingSide.Debit ? line.Amount : 0m)
            })
            .ToDictionaryAsync(item => item.AccountId, cancellationToken);

        var snapshots = accounts.Select(account =>
        {
            totals.TryGetValue(account.Id, out var total);
            return new ChangesInEquityAccountSnapshot(
                account.Id,
                account.Code,
                account.Name,
                account.Type,
                total?.OpeningDebits ?? 0m,
                total?.OpeningCredits ?? 0m,
                total?.PeriodDebits ?? 0m,
                total?.PeriodCredits ?? 0m,
                total?.ClosingDebits ?? 0m,
                total?.ClosingCredits ?? 0m,
                (total?.OwnerContributionCredits ?? 0m) - (total?.OwnerContributionDebits ?? 0m),
                (total?.OwnerDistributionDebits ?? 0m) - (total?.OwnerDistributionCredits ?? 0m),
                total?.TransfersIn ?? 0m,
                total?.TransfersOut ?? 0m,
                total?.OtherIncreases ?? 0m,
                total?.OtherDecreases ?? 0m,
                total?.UnclassifiedIncreases ?? 0m,
                total?.UnclassifiedDecreases ?? 0m);
        }).ToArray();

        decimal UnclosedNetIncome(bool opening) =>
            snapshots.Where(account => account.Type is AccountType.Income or AccountType.Expense)
                .Sum(account => opening
                    ? account.OpeningCredits - account.OpeningDebits
                    : account.ClosingCredits - account.ClosingDebits);

        return new ChangesInEquitySnapshot(
            snapshots,
            UnclosedNetIncome(opening: true),
            UnclosedNetIncome(opening: false));
    }
}
