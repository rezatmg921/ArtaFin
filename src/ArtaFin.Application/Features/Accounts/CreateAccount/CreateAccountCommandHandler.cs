using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Features.Accounts.CreateAccount;

/// <summary>
/// Orchestrates account creation across the FinancialBook and Account aggregates.
/// Cross-aggregate ownership and uniqueness checks belong here, not inside Account.
/// </summary>
public sealed class CreateAccountCommandHandler(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    IAccountRepository accountRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Guid> HandleAsync(
        CreateAccountCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.FinancialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(command));

        var financialBook = await financialBookRepository.GetByIdAsync(
            command.FinancialBookId,
            cancellationToken);

        if (financialBook is null)
            throw new FinancialBookAccessDeniedException();

        var userId = currentUser.UserId;
        if (!financialBook.IsOwnedBy(userId))
        {
            var membership = await financialBookRepository.GetMemberAsync(
                financialBook.Id,
                userId,
                cancellationToken);

            if (membership is null ||
                !membership.CanCreateAccountsAt(timeProvider.GetUtcNow()))
            {
                throw new FinancialBookAccessDeniedException();
            }
        }

        var normalizedCode = command.Code?.Trim().ToUpperInvariant()
            ?? throw new ArgumentNullException(nameof(command.Code));

        if (await accountRepository.ExistsByCodeAsync(
                financialBook.Id, normalizedCode, cancellationToken))
        {
            throw new AccountCodeAlreadyExistsException(normalizedCode);
        }

        var account = Account.Create(
            Guid.NewGuid(),
            financialBook.Id,
            normalizedCode,
            command.Name,
            command.Type,
            timeProvider.GetUtcNow());

        await accountRepository.AddAsync(account, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return account.Id;
    }
}
