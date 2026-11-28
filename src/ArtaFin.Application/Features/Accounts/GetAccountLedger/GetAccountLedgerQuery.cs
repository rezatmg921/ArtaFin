using ArtaFin.Domain.Finance;
using MediatR;

namespace ArtaFin.Application.Features.Accounts.GetAccountLedger;

public sealed record GetAccountLedgerQuery(
    Guid FinancialBookId,
    Guid AccountId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page,
    int PageSize) : IRequest<AccountLedgerResult>;

public sealed record AccountLedgerResult(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    AccountType AccountType,
    AccountNormalBalance NormalBalance,
    string Currency,
    int Page,
    int PageSize,
    int TotalCount,
    decimal OpeningBalance,
    IReadOnlyList<AccountLedgerLineResult> Items);

public sealed record AccountLedgerLineResult(
    Guid JournalEntryId,
    DateTimeOffset PostedAt,
    string Description,
    Guid? ReversesJournalEntryId,
    int LineNumber,
    PostingSide Side,
    decimal Amount,
    string Currency,
    decimal BalanceAfter);
