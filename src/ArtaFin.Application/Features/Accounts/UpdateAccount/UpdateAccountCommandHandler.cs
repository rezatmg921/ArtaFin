using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Accounts.CreateAccount;
using ArtaFin.Domain.Finance;
using MediatR;

namespace ArtaFin.Application.Features.Accounts.UpdateAccount;

/// <summary>
/// Renames an account after checking book access and the client's If-Match condition.
/// The database concurrency token protects the interval between the read and the commit.
/// </summary>
public sealed class UpdateAccountCommandHandler(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    IAccountRepository accountRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateAccountCommand, AccountReadSnapshot?>
{
    public async Task<AccountReadSnapshot?> Handle(
        UpdateAccountCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.FinancialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(command));
        if (command.AccountId == Guid.Empty)
            throw new ArgumentException("Account id cannot be empty.", nameof(command));

        var book = await financialBookRepository.GetByIdAsync(
            command.FinancialBookId,
            cancellationToken);

        if (book is null)
            throw new FinancialBookAccessDeniedException();

        if (!book.IsOwnedBy(currentUser.UserId))
        {
            var membership = await financialBookRepository.GetMemberAsync(
                book.Id,
                currentUser.UserId,
                cancellationToken);

            if (membership is null ||
                !membership.CanCreateAccountsAt(timeProvider.GetUtcNow()))
            {
                throw new FinancialBookAccessDeniedException();
            }
        }

        // Keep EF's loaded RowVersion as OriginalValue. It will detect a race
        // even when the client supplied multiple acceptable entity tags.
        var snapshot = await accountRepository.GetForUpdateAsync(
            book.Id,
            command.AccountId,
            expectedRowVersion: null,
            cancellationToken);

        if (snapshot is null)
            return null;

        if (!command.MatchAny &&
            (command.ExpectedRowVersions is null ||
             !command.ExpectedRowVersions.Any(expected =>
                 snapshot.RowVersion.AsSpan().SequenceEqual(expected))))
        {
            throw new AccountPreconditionFailedException();
        }

        snapshot.Account.Rename(command.Name);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Read the database-generated row version after SaveChanges so the caller
        // receives the new ETag, not the version observed before the update.
        return await accountRepository.GetReadSnapshotByIdAsync(
            book.Id,
            command.AccountId,
            cancellationToken);
    }
}
