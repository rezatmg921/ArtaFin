using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Accounts.CreateAccount;
using ArtaFin.Application.Features.Accounts.UpdateAccount;
using MediatR;

namespace ArtaFin.Application.Features.Accounts.DeleteAccount;

/// <summary>
/// Deletes an account only when it has no journal history and its ETag is current.
/// </summary>
public sealed class DeleteAccountCommandHandler(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    IAccountRepository accountRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<DeleteAccountCommand, bool>
{
    public async Task<bool> Handle(
        DeleteAccountCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.FinancialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(command));
        if (command.AccountId == Guid.Empty)
            throw new ArgumentException("Account id cannot be empty.", nameof(command));

        var book = await financialBookRepository.GetByIdAsync(
            command.FinancialBookId, cancellationToken);

        if (book is null)
            throw new FinancialBookAccessDeniedException();

        if (!book.IsOwnedBy(currentUser.UserId))
        {
            var membership = await financialBookRepository.GetMemberAsync(
                book.Id, currentUser.UserId, cancellationToken);

            if (membership is null ||
                !membership.CanCreateAccountsAt(timeProvider.GetUtcNow()))
            {
                throw new FinancialBookAccessDeniedException();
            }
        }

        // Load a tracked aggregate and retain its database RowVersion as EF's
        // original value. EF will detect a concurrent update at SaveChanges.
        var snapshot = await accountRepository.GetForUpdateAsync(
            book.Id, command.AccountId, expectedRowVersion: null, cancellationToken);

        if (snapshot is null)
            return false;

        if (!command.MatchAny &&
            (command.ExpectedRowVersions is null ||
             !command.ExpectedRowVersions.Any(expected =>
                 snapshot.RowVersion.AsSpan().SequenceEqual(expected))))
        {
            throw new AccountPreconditionFailedException();
        }

        var deleted = await accountRepository.DeleteIfUnusedAsync(
            snapshot.Account, cancellationToken);

        if (!deleted)
            throw new AccountHasJournalEntriesException();

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}