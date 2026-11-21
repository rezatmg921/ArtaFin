using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.FinancialBooks.InviteMember;
using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Features.FinancialBooks.ManageMember;

/// <summary>
/// Allows only the financial-book owner to change an active member's role.
/// </summary>
public sealed class ChangeFinancialBookMemberRoleCommandHandler(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(
        ChangeFinancialBookMemberRoleCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.FinancialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(command));
        if (command.MembershipId == Guid.Empty)
            throw new ArgumentException("Membership id cannot be empty.", nameof(command));
        if (!Enum.IsDefined(command.Role))
            throw new ArgumentOutOfRangeException(nameof(command.Role));

        var book = await financialBookRepository.GetByIdAsync(
            command.FinancialBookId, cancellationToken);

        if (book is null || !book.IsOwnedBy(currentUser.UserId))
            throw new FinancialBookManagementDeniedException();

        var member = await financialBookRepository.GetMemberByIdAsync(
            book.Id, command.MembershipId, cancellationToken);

        if (member is null)
            throw new FinancialBookMemberNotFoundException();

        if (member.RevokedAt is not null)
            throw new FinancialBookMemberNotActiveException();

        member.ChangeRole(command.Role);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
