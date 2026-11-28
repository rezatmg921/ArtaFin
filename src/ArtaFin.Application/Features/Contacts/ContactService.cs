using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Domain.Common;
using ArtaFin.Domain.Contacts;
using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Features.Contacts;

public sealed record ContactSnapshot(
    Guid Id, Guid FinancialBookId, ContactType Type, string DisplayName,
    string? Email, string? Phone, string? Notes, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, DateTimeOffset? ArchivedAt);

/// <summary>Use cases for contacts, always scoped to a FinancialBook.</summary>
public sealed class ContactService(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    IContactRepository contactRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Guid> CreateAsync(
        Guid financialBookId, ContactType type, string displayName,
        string? email, string? phone, string? notes, CancellationToken cancellationToken)
    {
        await EnsureBookAccessAsync(financialBookId, write: true, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var contact = Contact.Create(Guid.NewGuid(), financialBookId, type,
            displayName, email, phone, notes, now);
        await contactRepository.AddAsync(contact, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return contact.Id;
    }

    public async Task<IReadOnlyList<ContactSnapshot>> ListAsync(
        Guid financialBookId, bool includeArchived, int skip, int take,
        CancellationToken cancellationToken)
    {
        await EnsureBookAccessAsync(financialBookId, write: false, cancellationToken);
        if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip));
        if (take is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(take));
        var contacts = await contactRepository.ListAsync(
            financialBookId, includeArchived, skip, take, cancellationToken);
        return contacts.Select(ToSnapshot).ToArray();
    }

    public async Task<ContactSnapshot?> GetByIdAsync(
        Guid financialBookId, Guid contactId, CancellationToken cancellationToken)
    {
        await EnsureBookAccessAsync(financialBookId, write: false, cancellationToken);
        var contact = await contactRepository.GetByIdAsync(
            financialBookId, contactId, cancellationToken);
        return contact is null ? null : ToSnapshot(contact);
    }

    public async Task UpdateAsync(
        Guid financialBookId, Guid contactId, ContactType type, string displayName,
        string? email, string? phone, string? notes, CancellationToken cancellationToken)
    {
        await EnsureBookAccessAsync(financialBookId, write: true, cancellationToken);
        var contact = await contactRepository.GetByIdAsync(
            financialBookId, contactId, cancellationToken);
        if (contact is null) throw new KeyNotFoundException("Contact was not found.");
        contact.UpdateDetails(type, displayName, email, phone, notes, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ArchiveAsync(
        Guid financialBookId, Guid contactId, CancellationToken cancellationToken)
    {
        await EnsureBookAccessAsync(financialBookId, write: true, cancellationToken);
        var contact = await contactRepository.GetByIdAsync(
            financialBookId, contactId, cancellationToken);
        if (contact is null) throw new KeyNotFoundException("Contact was not found.");
        contact.Archive(timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureBookAccessAsync(
        Guid financialBookId, bool write, CancellationToken cancellationToken)
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
            throw new UnauthorizedAccessException("Accountant membership is required to modify contacts.");
    }

    private static ContactSnapshot ToSnapshot(Contact contact) => new(
        contact.Id, contact.FinancialBookId, contact.Type, contact.DisplayName,
        contact.Email, contact.Phone, contact.Notes, contact.CreatedAt,
        contact.UpdatedAt, contact.ArchivedAt);
}
