using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Accounts.CreateAccount;
using MediatR;

namespace ArtaFin.Application.Features.Accounts.GetAccountLedger;

/// <summary>
/// Returns a paged account ledger for an owner or active financial-book member.
/// Balances follow the account's normal-balance convention.
/// </summary>
public sealed class GetAccountLedgerQueryHandler(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    IAccountRepository accountRepository,
    IAccountLedgerReadRepository ledgerReadRepository,
    TimeProvider timeProvider)
    : IRequestHandler<GetAccountLedgerQuery, AccountLedgerResult>
{
    public async Task<AccountLedgerResult> Handle(
        GetAccountLedgerQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.FinancialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(query));
        if (query.AccountId == Guid.Empty)
            throw new ArgumentException("Account id cannot be empty.", nameof(query));
        if (query.Page < 1)
            throw new ArgumentOutOfRangeException(nameof(query), "Page must be greater than zero.");
        if (query.PageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(query), "Page size must be between 1 and 100.");
        if (query.Page > int.MaxValue / query.PageSize)
            throw new ArgumentOutOfRangeException(nameof(query), "Requested page is too large.");
        if (query.From.HasValue && query.To.HasValue && query.From > query.To)
            throw new ArgumentException("From date cannot be later than To date.", nameof(query));

        var book = await financialBookRepository.GetByIdAsync(
            query.FinancialBookId, cancellationToken);

        if (book is null)
            throw new FinancialBookAccessDeniedException();

        if (!book.IsOwnedBy(currentUser.UserId))
        {
            var membership = await financialBookRepository.GetMemberAsync(
                book.Id, currentUser.UserId, cancellationToken);

            if (membership is null || !membership.IsActiveAt(timeProvider.GetUtcNow()))
                throw new FinancialBookAccessDeniedException();
        }

        var account = await accountRepository.GetByIdAsync(
            book.Id, query.AccountId, cancellationToken);

        if (account is null)
            throw new AccountLedgerNotFoundException();

        var page = await ledgerReadRepository.GetPageAsync(
            book.Id,
            account.Id,
            account.NormalBalance,
            query.From,
            query.To,
            query.Page,
            query.PageSize,
            cancellationToken);

        return new AccountLedgerResult(
            account.Id,
            account.Code,
            account.Name,
            account.Type,
            account.NormalBalance,
            book.BaseCurrency.Code,
            query.Page,
            query.PageSize,
            page.TotalCount,
            page.OpeningBalance,
            page.Items.Select(line => new AccountLedgerLineResult(
                line.JournalEntryId,
                line.PostedAt,
                line.Description,
                line.ReversesJournalEntryId,
                line.LineNumber,
                line.Side,
                line.Amount,
                line.Currency,
                line.BalanceAfter)).ToArray());
    }
}
