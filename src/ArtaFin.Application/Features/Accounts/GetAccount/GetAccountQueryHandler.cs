using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Accounts.CreateAccount;
using MediatR;

namespace ArtaFin.Application.Features.Accounts.GetAccount;

/// <summary>
/// Loads an account only when the caller owns or is an active member
/// of the requested financial book.
/// </summary>
public sealed class GetAccountQueryHandler(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    IAccountRepository accountRepository,
    TimeProvider timeProvider)
    : IRequestHandler<GetAccountQuery, AccountReadSnapshot?>
{
    public async Task<AccountReadSnapshot?> Handle(
        GetAccountQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.FinancialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(query));
        if (query.AccountId == Guid.Empty)
            throw new ArgumentException("Account id cannot be empty.", nameof(query));

        var book = await financialBookRepository.GetByIdAsync(
            query.FinancialBookId,
            cancellationToken);

        if (book is null)
            throw new FinancialBookAccessDeniedException();

        if (!book.IsOwnedBy(currentUser.UserId))
        {
            var membership = await financialBookRepository.GetMemberAsync(
                book.Id,
                currentUser.UserId,
                cancellationToken);

            if (membership is null || !membership.IsActiveAt(timeProvider.GetUtcNow()))
                throw new FinancialBookAccessDeniedException();
        }

        return await accountRepository.GetReadSnapshotByIdAsync(
            book.Id,
            query.AccountId,
            cancellationToken);
    }
}
