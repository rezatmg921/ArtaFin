using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Features.CashFlowNonCashDisclosures;
using ArtaFin.Domain.Finance;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Infrastructure.Persistence.Repositories;

internal sealed class CashFlowNonCashDisclosureRepository(ArtaFinDbContext dbContext)
    : ICashFlowNonCashDisclosureRepository
{
    private const string UniqueSourceIndex = "UX_CashFlowNonCashDisclosures_SourceLine";

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

    public async Task AddAsync(
        CashFlowNonCashDisclosure disclosure,
        CancellationToken cancellationToken)
    {
        await dbContext.CashFlowNonCashDisclosures.AddAsync(disclosure, cancellationToken);
    }

    public async Task<IReadOnlyList<CashFlowNonCashDisclosure>> GetByJournalEntryAsync(
        Guid financialBookId,
        Guid journalEntryId,
        CancellationToken cancellationToken) =>
        await dbContext.CashFlowNonCashDisclosures
            .AsNoTracking()
            .Where(item => item.FinancialBookId == financialBookId &&
                           item.JournalEntryId == journalEntryId)
            .OrderBy(item => item.SourceLineNumber)
            .ToListAsync(cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateSource(exception))
        {
            throw new CashFlowNonCashDisclosureAlreadyExistsException();
        }
    }

    private static bool IsDuplicateSource(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is not SqlException sqlException)
                continue;

            foreach (SqlError error in sqlException.Errors)
            {
                if (error.Number is 2601 or 2627 &&
                    error.Message.Contains(UniqueSourceIndex, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }
}
