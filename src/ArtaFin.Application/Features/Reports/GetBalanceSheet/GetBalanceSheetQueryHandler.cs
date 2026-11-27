using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Accounts.CreateAccount;
using ArtaFin.Domain.Finance;
using ArtaFin.Domain.Obligations;
using MediatR;

namespace ArtaFin.Application.Features.Reports.GetBalanceSheet;

public sealed class GetBalanceSheetQueryHandler(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    IBalanceSheetReadRepository balanceSheetReadRepository,
    IObligationMaturityReadRepository obligationMaturityReadRepository,
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

        var unclosedNetIncome = snapshots.Sum(account => account.Type switch
        {
            AccountType.Income => account.TotalCredits - account.TotalDebits,
            AccountType.Expense => account.TotalCredits - account.TotalDebits,
            _ => 0m
        });

        var obligationSnapshots = await obligationMaturityReadRepository.GetAsOfAsync(
            book.Id, asOf, cancellationToken);
        var accountById = snapshots.ToDictionary(account => account.AccountId);
        var obligationMaturities = obligationSnapshots
            .GroupBy(snapshot => new
            {
                snapshot.ControlAccountId,
                snapshot.Direction,
                snapshot.Currency
            })
            .Select(group =>
            {
                var installments = group.SelectMany(snapshot => snapshot.Installments).ToArray();
                var payments = group.SelectMany(snapshot => snapshot.Payments).ToArray();
                var maturity = ObligationMaturityCalculator.Calculate(
                    DateOnly.FromDateTime(asOf.DateTime), installments, payments);

                var expectedAccountType = group.Key.Direction == FinancialObligationDirection.Payable
                    ? AccountType.Liability
                    : AccountType.Asset;
                var hasMatchingAccount = accountById.TryGetValue(
                    group.Key.ControlAccountId, out var account);
                var ledgerBalance = hasMatchingAccount &&
                                    account!.Type == expectedAccountType &&
                                    string.Equals(group.Key.Currency, book.BaseCurrency.Code, StringComparison.Ordinal)
                    ? NormalBalanceAmount(account!)
                    : 0m;
                var isReconciled = hasMatchingAccount &&
                    account!.Type == expectedAccountType &&
                    string.Equals(group.Key.Currency, book.BaseCurrency.Code, StringComparison.Ordinal) &&
                    ledgerBalance == maturity.TotalOutstanding;

                return new BalanceSheetObligationMaturityResult(
                    group.Key.ControlAccountId,
                    group.Key.Direction,
                    group.Key.Currency,
                    maturity.CurrentAmount,
                    maturity.NonCurrentAmount,
                    maturity.TotalOutstanding,
                    ledgerBalance,
                    maturity.TotalOutstanding - ledgerBalance,
                    isReconciled);
            })
            .OrderBy(result => result.Direction)
            .ThenBy(result => result.ControlAccountId)
            .ToArray();

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
            totalAssets - totalLiabilitiesAndEquity,
            obligationMaturities);
    }

    private static BalanceSheetAccountResult ToResult(
        BalanceSheetAccountSnapshot account,
        decimal amount) =>
        new(account.AccountId, account.Code, account.Name, account.Type, amount);
}
