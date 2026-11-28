using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Domain.Contacts;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Infrastructure.Persistence.Repositories;

internal sealed class ContactRepository(ArtaFinDbContext dbContext) : IContactRepository
{
    public Task<Contact?> GetByIdAsync(
        Guid financialBookId, Guid contactId, CancellationToken cancellationToken) =>
        dbContext.Contacts.SingleOrDefaultAsync(
            contact => contact.FinancialBookId == financialBookId && contact.Id == contactId,
            cancellationToken);

    public async Task<IReadOnlyList<Contact>> ListAsync(
        Guid financialBookId, bool includeArchived, int skip, int take,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Contacts.AsNoTracking()
            .Where(contact => contact.FinancialBookId == financialBookId);
        if (!includeArchived)
            query = query.Where(contact => contact.ArchivedAt == null);

        return await query.OrderBy(contact => contact.DisplayName)
            .ThenBy(contact => contact.Id)
            .Skip(skip).Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Contact contact, CancellationToken cancellationToken) =>
        await dbContext.Contacts.AddAsync(contact, cancellationToken);
}
