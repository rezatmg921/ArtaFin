using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;

namespace ArtaFin.Application.Features.FinancialBooks.ManageMember;

/// <summary>
/// Allows only the financial-book owner to revoke an active member without deleting its history.
/// </summary>
public sealed class RevokeFinancialBookMemberCommandHandler(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task HandleAsync(
        RevokeFinancialBookMemberCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.FinancialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(command));
        if (command.MembershipId == Guid.Empty)
            throw new ArgumentException("Membership id cannot be empty.", nameof(command));

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

        member.Revoke(timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class FinancialBookMemberNotFoundException : Exception { }
public sealed class FinancialBookMemberNotActiveException : Exception { }
