using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Features.FinancialBooks.CreateFinancialBook;

/// <summary>Creates a financial book owned by the authenticated user.</summary>
public sealed class CreateFinancialBookCommandHandler(
    ICurrentUser currentUser,
    IFinancialBookWriter financialBookWriter,
    IAccountRepository accountRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Guid> HandleAsync(
        CreateFinancialBookCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (currentUser.UserId == Guid.Empty)
            throw new InvalidOperationException("An authenticated user is required.");

        var financialBook = FinancialBook.Create(
            Guid.NewGuid(),
            currentUser.UserId,
            command.Name,
            command.BaseCurrency,
            timeProvider.GetUtcNow());

        await financialBookWriter.AddAsync(financialBook, cancellationToken);

        // The book and its initial accounts are tracked by the same unit of work.
        // A single SaveChanges keeps their insertion atomic in the EF Core transaction.
        await StandardChartOfAccounts.SeedAsync(
            financialBook.Id,
            accountRepository,
            timeProvider,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return financialBook.Id;
    }
}
