using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Accounts.CreateAccount;
using ArtaFin.Application.Features.Accounts.UpdateAccount;
using ArtaFin.Domain.Finance;
using MediatR;

namespace ArtaFin.Application.Features.Accounts.ChangeAccountStatus;

/// <summary>
/// Changes an account's active state while enforcing authorization, If-Match,
/// and database row-version concurrency.
/// </summary>
public sealed class ChangeAccountStatusCommandHandler(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    IAccountRepository accountRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<ChangeAccountStatusCommand, AccountReadSnapshot?>
{
    public async Task<AccountReadSnapshot?> Handle(
        ChangeAccountStatusCommand command,
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

        // Load a tracked account and retain EF's original RowVersion for race detection.
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

        var now = timeProvider.GetUtcNow();
        if (command.Reactivate)
            snapshot.Account.Reactivate(now);
        else
            snapshot.Account.Deactivate(now);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // SQL Server generates the new rowversion during save; return its new ETag.
        return await accountRepository.GetReadSnapshotByIdAsync(
            book.Id,
            command.AccountId,
            cancellationToken);
    }
}
