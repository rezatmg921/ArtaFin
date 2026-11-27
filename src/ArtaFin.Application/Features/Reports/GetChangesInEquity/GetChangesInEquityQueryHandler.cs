using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Accounts.CreateAccount;
using ArtaFin.Domain.Finance;
using MediatR;

namespace ArtaFin.Application.Features.Reports.GetChangesInEquity;

public sealed class GetChangesInEquityQueryHandler(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    IChangesInEquityReadRepository readRepository,
    TimeProvider timeProvider)
    : IRequestHandler<GetChangesInEquityQuery, ChangesInEquityResult>
{
    public async Task<ChangesInEquityResult> Handle(
        GetChangesInEquityQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.FinancialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(query));
        if (query.From >= query.To)
            throw new ArgumentException("From must be earlier than To.", nameof(query));

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

        var snapshot = await readRepository.GetPeriodAsync(
            book.Id, query.From, query.To, cancellationToken);

        var components = snapshot.Accounts
            .Where(account => account.Type == AccountType.Equity)
            .Select(account => new ChangesInEquityAccountResult(
                account.AccountId,
                account.Code,
                account.Name,
                account.OpeningCredits - account.OpeningDebits,
                account.PeriodCredits,
                account.PeriodDebits,
                account.ClosingCredits - account.ClosingDebits,
                account.OwnerContributions,
                account.OwnerDistributions,
                account.TransfersIn,
                account.TransfersOut,
                account.OtherIncreases,
                account.OtherDecreases,
                account.UnclassifiedIncreases,
                account.UnclassifiedDecreases))
            .ToArray();

        var openingEquity = components.Sum(account => account.OpeningBalance);
        var totalIncreases = components.Sum(account => account.Increases);
        var totalDecreases = components.Sum(account => account.Decreases);
        var closingEquity = components.Sum(account => account.ClosingBalance);

        return new ChangesInEquityResult(
            query.From,
            query.To,
            book.BaseCurrency.Code,
            components,
            openingEquity,
            totalIncreases,
            totalDecreases,
            closingEquity,
            snapshot.OpeningUnclosedNetIncome,
            snapshot.ClosingUnclosedNetIncome,
            openingEquity + snapshot.OpeningUnclosedNetIncome,
            closingEquity + snapshot.ClosingUnclosedNetIncome);
    }
}
