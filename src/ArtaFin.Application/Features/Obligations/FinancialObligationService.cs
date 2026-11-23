using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Domain.Common;
using ArtaFin.Domain.Finance;
using ArtaFin.Domain.Obligations;

namespace ArtaFin.Application.Features.Obligations;

public sealed record FinancialObligationSnapshot(
    Guid Id, Guid FinancialBookId, Guid ContactId, Guid ControlAccountId,
    FinancialObligationDirection Direction, FinancialObligationKind Kind,
    decimal OriginalAmount, string Currency, DateOnly EffectiveDate, DateOnly DueDate,
    string Description, string? Reference, FinancialObligationStatus Status,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? CancelledAt);

/// <summary>Book-scoped use cases for creating, reading and cancelling obligations.</summary>
public sealed class FinancialObligationService(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    IContactRepository contactRepository,
    IAccountRepository accountRepository,
    IFinancialObligationRepository obligationRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Guid> CreateAsync(
        Guid financialBookId, Guid contactId, Guid controlAccountId,
        FinancialObligationDirection direction, FinancialObligationKind kind,
        decimal amount, string currency, DateOnly effectiveDate, DateOnly dueDate,
        string description, string? reference, CancellationToken cancellationToken)
    {
        await EnsureBookAccessAsync(financialBookId, write: true, cancellationToken);

        var contact = await contactRepository.GetByIdAsync(financialBookId, contactId, cancellationToken);
        if (contact is null || contact.IsArchived)
            throw new KeyNotFoundException("An active contact in this financial book was not found.");

        var account = await accountRepository.GetByIdAsync(financialBookId, controlAccountId, cancellationToken);
        if (account is null || account.Status != AccountStatus.Active)
            throw new KeyNotFoundException("An active control account in this financial book was not found.");

        var expectedAccountType = direction switch
        {
            FinancialObligationDirection.Receivable => AccountType.Asset,
            FinancialObligationDirection.Payable => AccountType.Liability,
            _ => throw new ArgumentOutOfRangeException(nameof(direction))
        };
        if (account.Type != expectedAccountType)
            throw new DomainException(direction == FinancialObligationDirection.Receivable
                ? "A receivable must use an asset control account."
                : "A payable must use a liability control account.");

        var now = timeProvider.GetUtcNow();
        var obligation = FinancialObligation.Create(
            Guid.NewGuid(), financialBookId, contactId, controlAccountId,
            direction, kind, amount, currency, effectiveDate, dueDate,
            description, reference, now);
        await obligationRepository.AddAsync(obligation, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return obligation.Id;
    }

    public async Task<IReadOnlyList<FinancialObligationSnapshot>> ListAsync(
        Guid financialBookId, FinancialObligationDirection? direction,
        FinancialObligationStatus? status, int skip, int take, CancellationToken cancellationToken)
    {
        await EnsureBookAccessAsync(financialBookId, write: false, cancellationToken);
        if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip));
        if (take is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(take));
        var items = await obligationRepository.ListAsync(
            financialBookId, direction, status, skip, take, cancellationToken);
        return items.Select(ToSnapshot).ToArray();
    }

    public async Task<FinancialObligationSnapshot?> GetByIdAsync(
        Guid financialBookId, Guid obligationId, CancellationToken cancellationToken)
    {
        await EnsureBookAccessAsync(financialBookId, write: false, cancellationToken);
        var item = await obligationRepository.GetByIdAsync(financialBookId, obligationId, cancellationToken);
        return item is null ? null : ToSnapshot(item);
    }

    public async Task CancelAsync(
        Guid financialBookId, Guid obligationId, CancellationToken cancellationToken)
    {
        await EnsureBookAccessAsync(financialBookId, write: true, cancellationToken);
        var item = await obligationRepository.GetByIdAsync(financialBookId, obligationId, cancellationToken);
        if (item is null) throw new KeyNotFoundException("Financial obligation was not found.");
        item.Cancel(timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureBookAccessAsync(Guid financialBookId, bool write, CancellationToken cancellationToken)
    {
        if (currentUser.UserId == Guid.Empty)
            throw new UnauthorizedAccessException("An authenticated user is required.");
        var book = await financialBookRepository.GetByIdAsync(financialBookId, cancellationToken);
        if (book is null) throw new KeyNotFoundException("Financial book was not found.");
        if (book.IsOwnedBy(currentUser.UserId)) return;
        var member = await financialBookRepository.GetMemberAsync(
            financialBookId, currentUser.UserId, cancellationToken);
        if (member is null || !member.IsActiveAt(timeProvider.GetUtcNow()))
            throw new UnauthorizedAccessException("The user does not have access to this financial book.");
        if (write && member.Role != FinancialBookRole.Accountant)
            throw new UnauthorizedAccessException("Accountant membership is required to modify financial obligations.");
    }

    private static FinancialObligationSnapshot ToSnapshot(FinancialObligation x) => new(
        x.Id, x.FinancialBookId, x.ContactId, x.ControlAccountId, x.Direction, x.Kind,
        x.OriginalAmount, x.Currency, x.EffectiveDate, x.DueDate, x.Description,
        x.Reference, x.Status, x.CreatedAt, x.UpdatedAt, x.CancelledAt);
}
