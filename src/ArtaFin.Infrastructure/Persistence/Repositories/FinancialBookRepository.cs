using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Infrastructure.Persistence.Repositories;

internal sealed class FinancialBookRepository(ArtaFinDbContext dbContext)
    : IFinancialBookRepository
{
    public Task<FinancialBook?> GetByIdAsync(
        Guid financialBookId,
        CancellationToken cancellationToken)
    {
        return dbContext.FinancialBooks
            .AsNoTracking()
            .SingleOrDefaultAsync(
                book => book.Id == financialBookId,
                cancellationToken);
    }

    public Task<FinancialBookMember?> GetMemberAsync(
        Guid financialBookId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return dbContext.FinancialBookMembers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                member => member.FinancialBookId == financialBookId &&
                          member.UserId == userId,
                cancellationToken);
    }
}
