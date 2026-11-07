using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Features.FinancialBooks.InviteMember;

/// <summary>
/// Allows only the financial-book owner to grant an active local user a book-scoped role.
/// </summary>
public sealed class InviteFinancialBookMemberCommandHandler(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Guid> HandleAsync(
        InviteFinancialBookMemberCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.FinancialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(command));
        if (string.IsNullOrWhiteSpace(command.Email))
            throw new ArgumentException("Email is required.", nameof(command));
        if (!Enum.IsDefined(command.Role))
            throw new ArgumentOutOfRangeException(nameof(command.Role));

        var book = await financialBookRepository.GetByIdAsync(
            command.FinancialBookId, cancellationToken);

        if (book is null || !book.IsOwnedBy(currentUser.UserId))
            throw new FinancialBookManagementDeniedException();

        var normalizedEmail = command.Email.Trim().ToUpperInvariant();
        var user = await userRepository.GetByNormalizedEmailAsync(
            normalizedEmail, cancellationToken);

        if (user is null || !user.IsActive)
            throw new InviteTargetUserNotFoundException();

        if (book.IsOwnedBy(user.Id))
            throw new FinancialBookMemberAlreadyExistsException();

        var existingMember = await financialBookRepository.GetMemberAsync(
            book.Id, user.Id, cancellationToken);

        if (existingMember is not null)
            throw new FinancialBookMemberAlreadyExistsException();

        var member = FinancialBookMember.Grant(
            Guid.NewGuid(),
            book.Id,
            user.Id,
            command.Role,
            timeProvider.GetUtcNow());

        await financialBookRepository.AddMemberAsync(member, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return member.Id;
    }
}

public sealed class FinancialBookManagementDeniedException : Exception { }
public sealed class InviteTargetUserNotFoundException : Exception { }
public sealed class FinancialBookMemberAlreadyExistsException : Exception { }
