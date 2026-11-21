using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Accounts.CreateAccount;
using ArtaFin.Domain.Finance;
using MediatR;

namespace ArtaFin.Application.Features.Reports.GetIncomeStatement;

public sealed class GetIncomeStatementQueryHandler(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    IIncomeStatementReadRepository incomeStatementReadRepository,
    TimeProvider timeProvider)
    : IRequestHandler<GetIncomeStatementQuery, IncomeStatementResult>
{
    public async Task<IncomeStatementResult> Handle(
        GetIncomeStatementQuery query,
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

        var accounts = await incomeStatementReadRepository.GetPeriodAsync(
            book.Id, query.From, query.To, cancellationToken);

        var incomeAccounts = accounts
            .Where(account => account.Type == AccountType.Income)
            .Select(account => new IncomeStatementAccountResult(
                account.AccountId,
                account.Code,
                account.Name,
                account.Type,
                account.TotalCredits - account.TotalDebits))
            .ToArray();

        var expenseAccounts = accounts
            .Where(account => account.Type == AccountType.Expense)
            .Select(account => new IncomeStatementAccountResult(
                account.AccountId,
                account.Code,
                account.Name,
                account.Type,
                account.TotalDebits - account.TotalCredits))
            .ToArray();

        var totalIncome = incomeAccounts.Sum(account => account.Amount);
        var totalExpenses = expenseAccounts.Sum(account => account.Amount);

        return new IncomeStatementResult(
            query.From,
            query.To,
            book.BaseCurrency.Code,
            incomeAccounts,
            expenseAccounts,
            totalIncome,
            totalExpenses,
            totalIncome - totalExpenses);
    }
}
