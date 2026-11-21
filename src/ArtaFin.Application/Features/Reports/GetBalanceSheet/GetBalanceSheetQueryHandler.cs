using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Accounts.CreateAccount;
using ArtaFin.Domain.Finance;
using MediatR;

namespace ArtaFin.Application.Features.Reports.GetBalanceSheet;

public sealed class GetBalanceSheetQueryHandler(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    IBalanceSheetReadRepository balanceSheetReadRepository,
    TimeProvider timeProvider)
    : IRequestHandler<GetBalanceSheetQuery, BalanceSheetResult>
{
    public async Task<BalanceSheetResult> Handle(
        GetBalanceSheetQuery query,
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

        var snapshots = await balanceSheetReadRepository.GetAsOfAsync(
            book.Id, asOf, cancellationToken);

        decimal NormalBalanceAmount(BalanceSheetAccountSnapshot account) =>
            account.Type switch
            {
                AccountType.Asset => account.TotalDebits - account.TotalCredits,
                AccountType.Liability or AccountType.Equity =>
                    account.TotalCredits - account.TotalDebits,
                _ => 0m
            };

        var assets = snapshots
            .Where(account => account.Type == AccountType.Asset)
            .Select(account => ToResult(account, NormalBalanceAmount(account)))
            .ToArray();

        var liabilities = snapshots
            .Where(account => account.Type == AccountType.Liability)
            .Select(account => ToResult(account, NormalBalanceAmount(account)))
            .ToArray();

        var equity = snapshots
            .Where(account => account.Type == AccountType.Equity)
            .Select(account => ToResult(account, NormalBalanceAmount(account)))
            .ToArray();

        // Income and expense balances represent earnings not yet transferred
        // to equity by closing entries. If the books have been closed, these
        // balances are zero and the retained earnings remain in Equity accounts.
        var unclosedNetIncome = snapshots.Sum(account => account.Type switch
        {
            AccountType.Income => account.TotalCredits - account.TotalDebits,
            AccountType.Expense => account.TotalCredits - account.TotalDebits,
            _ => 0m
        });

        var totalAssets = assets.Sum(account => account.Amount);
        var totalLiabilities = liabilities.Sum(account => account.Amount);
        var totalEquity = equity.Sum(account => account.Amount) + unclosedNetIncome;
        var totalLiabilitiesAndEquity = totalLiabilities + totalEquity;

        return new BalanceSheetResult(
            asOf,
            book.BaseCurrency.Code,
            assets,
            liabilities,
            equity,
            unclosedNetIncome,
            totalAssets,
            totalLiabilities,
            totalEquity,
            totalLiabilitiesAndEquity,
            totalAssets - totalLiabilitiesAndEquity);
    }

    private static BalanceSheetAccountResult ToResult(
        BalanceSheetAccountSnapshot account,
        decimal amount) =>
        new(account.AccountId, account.Code, account.Name, account.Type, amount);
}
