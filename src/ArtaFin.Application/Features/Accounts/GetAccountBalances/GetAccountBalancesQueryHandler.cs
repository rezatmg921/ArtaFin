using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Accounts.CreateAccount;
using MediatR;

namespace ArtaFin.Application.Features.Accounts.GetAccountBalances;

public sealed class GetAccountBalancesQueryHandler(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    IAccountBalanceReadRepository balanceReadRepository,
    TimeProvider timeProvider)
    : IRequestHandler<GetAccountBalancesQuery, AccountBalancesResult>
{
    public async Task<AccountBalancesResult> Handle(
        GetAccountBalancesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.FinancialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(query));
        if (query.Page < 1)
            throw new ArgumentOutOfRangeException(nameof(query), "Page must be greater than zero.");
        if (query.PageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(query), "Page size must be between 1 and 100.");
        if (query.Page > int.MaxValue / query.PageSize)
            throw new ArgumentOutOfRangeException(nameof(query), "Requested page is too large.");

        var book = await financialBookRepository.GetByIdAsync(
            query.FinancialBookId, cancellationToken);
        if (book is null)
            throw new FinancialBookAccessDeniedException();

        if (!book.IsOwnedBy(currentUser.UserId))
        {
            var member = await financialBookRepository.GetMemberAsync(
                book.Id, currentUser.UserId, cancellationToken);
            if (member is null || !member.IsActiveAt(timeProvider.GetUtcNow()))
                throw new FinancialBookAccessDeniedException();
        }

        var page = await balanceReadRepository.GetPageAsync(
            book.Id, query.Page, query.PageSize, cancellationToken);

        return new AccountBalancesResult(
            query.Page,
            query.PageSize,
            page.TotalCount,
            book.BaseCurrency.Code,
            page.Items.Select(item => new AccountBalanceResult(
                item.AccountId,
                item.Code,
                item.Name,
                item.Type,
                item.NormalBalance,
                item.Status,
                item.TotalDebits,
                item.TotalCredits,
                item.Balance)).ToArray());
    }
}
