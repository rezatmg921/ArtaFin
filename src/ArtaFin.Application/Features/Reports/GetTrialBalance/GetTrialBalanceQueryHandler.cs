using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Accounts.CreateAccount;
using MediatR;

namespace ArtaFin.Application.Features.Reports.GetTrialBalance;

public sealed class GetTrialBalanceQueryHandler(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    ITrialBalanceReadRepository trialBalanceReadRepository,
    TimeProvider timeProvider)
    : IRequestHandler<GetTrialBalanceQuery, TrialBalanceResult>
{
    public async Task<TrialBalanceResult> Handle(
        GetTrialBalanceQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.FinancialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(query));

        var asOf = query.AsOf ?? timeProvider.GetUtcNow();
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

        var accounts = await trialBalanceReadRepository.GetAsOfAsync(
            book.Id, asOf, cancellationToken);

        var items = accounts.Select(account =>
        {
            var netDebit = account.TotalDebits - account.TotalCredits;
            var debitBalance = Math.Max(netDebit, 0m);
            var creditBalance = Math.Max(-netDebit, 0m);

            return new TrialBalanceAccountResult(
                account.AccountId,
                account.Code,
                account.Name,
                account.Type,
                account.TotalDebits,
                account.TotalCredits,
                debitBalance,
                creditBalance);
        }).ToArray();

        return new TrialBalanceResult(
            asOf,
            book.BaseCurrency.Code,
            items,
            items.Sum(item => item.DebitBalance),
            items.Sum(item => item.CreditBalance));
    }
}
