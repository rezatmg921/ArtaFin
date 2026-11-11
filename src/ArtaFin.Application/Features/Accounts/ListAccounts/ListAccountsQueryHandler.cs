using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Accounts.CreateAccount;
using ArtaFin.Domain.Finance;
using MediatR;

namespace ArtaFin.Application.Features.Accounts.ListAccounts;

/// <summary>
/// Lists accounts only when the caller owns or is an active member
/// of the requested financial book. Read-only members may list accounts.
/// </summary>
public sealed class ListAccountsQueryHandler(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    IAccountReadRepository accountReadRepository,
    TimeProvider timeProvider)
    : IRequestHandler<ListAccountsQuery, AccountPage>
{
    public async Task<AccountPage> Handle(
        ListAccountsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.FinancialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(query));
        if (query.Page < 1)
            throw new ArgumentOutOfRangeException(nameof(query), "Page must be greater than zero.");
        if (query.PageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(query), "Page size must be between 1 and 100.");
        if (query.SearchTerm?.Trim().Length > 200)
            throw new ArgumentException("Search term cannot exceed 200 characters.", nameof(query));

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

        return await accountReadRepository.GetPageAsync(
            book.Id,
            query.Status,
            query.SearchTerm?.Trim(),
            query.Page,
            query.PageSize,
            cancellationToken);
    }
}
