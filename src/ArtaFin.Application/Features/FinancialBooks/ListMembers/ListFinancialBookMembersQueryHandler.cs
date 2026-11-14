using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.FinancialBooks.InviteMember;

namespace ArtaFin.Application.Features.FinancialBooks.ListMembers;

/// <summary>
/// Returns active members only to the owner of the requested financial book.
/// </summary>
public sealed class ListFinancialBookMembersQueryHandler(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository)
{
    public async Task<IReadOnlyList<FinancialBookMemberSummary>> HandleAsync(
        ListFinancialBookMembersQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.FinancialBookId == Guid.Empty)
            throw new ArgumentException(
                "Financial book id cannot be empty.", nameof(query));

        var book = await financialBookRepository.GetByIdAsync(
            query.FinancialBookId, cancellationToken);

        if (book is null || !book.IsOwnedBy(currentUser.UserId))
            throw new FinancialBookManagementDeniedException();

        return await financialBookRepository.GetActiveMembersAsync(
            book.Id, cancellationToken);
    }
}
