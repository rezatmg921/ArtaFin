using ArtaFin.Domain.Contacts;

namespace ArtaFin.Application.Abstractions.Persistence;

public interface IContactRepository
{
    Task<Contact?> GetByIdAsync(Guid financialBookId, Guid contactId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Contact>> ListAsync(
        Guid financialBookId, bool includeArchived, int skip, int take, CancellationToken cancellationToken);
    Task AddAsync(Contact contact, CancellationToken cancellationToken);
}
