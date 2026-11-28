using ArtaFin.Application.Abstractions.Persistence;

namespace ArtaFin.Application.Features.FinancialBooks.CreateFinancialBook;

/// <summary>
/// Backfills standard posting accounts into existing financial books.
/// Each book is processed in its own transaction so the operation can be safely retried.
/// </summary>
public sealed class SeedStandardAccountsForExistingBooksService(
    IFinancialBookIdReadRepository financialBookRepository,
    IAccountRepository accountRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<SeedStandardAccountsResult> SeedAsync(
        CancellationToken cancellationToken = default)
    {
        var financialBookIds = await financialBookRepository.GetAllIdsAsync(cancellationToken);
        var accountsCreated = 0;

        foreach (var financialBookId in financialBookIds)
        {
            var createdForBook = 0;

            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                createdForBook = await StandardChartOfAccounts.SeedAsync(
                    financialBookId,
                    accountRepository,
                    timeProvider,
                    token);

                await unitOfWork.SaveChangesAsync(token);
            }, cancellationToken);

            accountsCreated += createdForBook;
        }

        return new SeedStandardAccountsResult(financialBookIds.Count, accountsCreated);
    }
}

public sealed record SeedStandardAccountsResult(
    int FinancialBooksProcessed,
    int AccountsCreated);
