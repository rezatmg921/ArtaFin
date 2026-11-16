using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Abstractions.Persistence;

/// <summary>
/// Read-only port for account ledger lines and their cumulative normal-balance amount.
/// </summary>
public interface IAccountLedgerReadRepository
{
    Task<AccountLedgerPage> GetPageAsync(
        Guid financialBookId,
        Guid accountId,
        AccountNormalBalance normalBalance,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}

public sealed record AccountLedgerPage(
    IReadOnlyList<AccountLedgerLine> Items,
    int TotalCount,
    decimal OpeningBalance);

public sealed record AccountLedgerLine(
    Guid JournalEntryId,
    DateTimeOffset PostedAt,
    string Description,
    Guid? ReversesJournalEntryId,
    int LineNumber,
    PostingSide Side,
    decimal Amount,
    string Currency,
    decimal BalanceAfter);
