using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Domain.Finance;

namespace ArtaFin.Infrastructure.Persistence.Repositories;

internal sealed class JournalEntryRepository(ArtaFinDbContext dbContext)
    : IJournalEntryRepository
{
    public async Task AddAsync(JournalEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await dbContext.JournalEntries.AddAsync(entry, cancellationToken);
    }
}
