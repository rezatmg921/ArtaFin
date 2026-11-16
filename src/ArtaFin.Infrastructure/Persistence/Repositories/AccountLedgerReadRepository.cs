using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Infrastructure.Persistence.Repositories;

/// <summary>
/// Projects account activity from immutable posted journal lines. No balance is stored.
/// </summary>
internal sealed class AccountLedgerReadRepository(ArtaFinDbContext dbContext)
    : IAccountLedgerReadRepository
{
    public async Task<AccountLedgerPage> GetPageAsync(
        Guid financialBookId,
        Guid accountId,
        AccountNormalBalance normalBalance,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var allLines = dbContext.JournalEntries
            .AsNoTracking()
            .Where(entry => entry.FinancialBookId == financialBookId)
            .SelectMany(entry => entry.Lines.Select(line => new LedgerRow(
                entry.Id,
                entry.PostedAt,
                entry.Description,
                entry.ReversesJournalEntryId,
                line.LineNumber,
                line.AccountId,
                line.Side,
                EF.Property<decimal>(line, "_amount"),
                EF.Property<string>(line, "_currency"))))
            .Where(line => line.AccountId == accountId);

        var filteredLines = allLines;
        if (from.HasValue)
            filteredLines = filteredLines.Where(line => line.PostedAt >= from.Value);
        if (to.HasValue)
            filteredLines = filteredLines.Where(line => line.PostedAt <= to.Value);

        var totalCount = await filteredLines.CountAsync(cancellationToken);
        var rows = await filteredLines
            .OrderBy(line => line.PostedAt)
            .ThenBy(line => line.JournalEntryId)
            .ThenBy(line => line.LineNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return new AccountLedgerPage([], totalCount, 0m);

        var first = rows[0];
        var openingBalance = await allLines
            .Where(line =>
                line.PostedAt < first.PostedAt ||
                (line.PostedAt == first.PostedAt &&
                 (line.JournalEntryId.CompareTo(first.JournalEntryId) < 0 ||
                  (line.JournalEntryId == first.JournalEntryId &&
                   line.LineNumber < first.LineNumber))))
            .Select(line => line.Side == (PostingSide)normalBalance
                ? line.Amount
                : -line.Amount)
            .SumAsync(cancellationToken);

        var balance = openingBalance;
        var items = rows.Select(line =>
        {
            balance += line.Side == (PostingSide)normalBalance
                ? line.Amount
                : -line.Amount;

            return new AccountLedgerLine(
                line.JournalEntryId,
                line.PostedAt,
                line.Description,
                line.ReversesJournalEntryId,
                line.LineNumber,
                line.Side,
                line.Amount,
                line.Currency,
                balance);
        }).ToArray();

        return new AccountLedgerPage(items, totalCount, openingBalance);
    }

    private sealed record LedgerRow(
        Guid JournalEntryId,
        DateTimeOffset PostedAt,
        string Description,
        Guid? ReversesJournalEntryId,
        int LineNumber,
        Guid AccountId,
        PostingSide Side,
        decimal Amount,
        string Currency);
}
