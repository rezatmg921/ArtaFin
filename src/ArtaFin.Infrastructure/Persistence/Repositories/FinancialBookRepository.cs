using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Infrastructure.Persistence.Repositories;

internal sealed class FinancialBookRepository(ArtaFinDbContext dbContext)
    : IFinancialBookRepository, IFinancialBookWriter
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

    public async Task<IReadOnlyList<FinancialBookMemberSummary>> GetActiveMembersAsync(
        Guid financialBookId,
        CancellationToken cancellationToken)
    {
        return await (
            from member in dbContext.FinancialBookMembers.AsNoTracking()
            join user in dbContext.Users.AsNoTracking()
                on member.UserId equals user.Id
            where member.FinancialBookId == financialBookId &&
                  member.RevokedAt == null
            orderby member.GrantedAt, user.NormalizedEmail
            select new FinancialBookMemberSummary(
                member.Id,
                user.Id,
                user.Email,
                member.Role,
                member.GrantedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task AddMemberAsync(
        FinancialBookMember member,
        CancellationToken cancellationToken)
    {
        await dbContext.FinancialBookMembers.AddAsync(member, cancellationToken);
    }

    public async Task AddAsync(
        FinancialBook financialBook,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(financialBook);
        await dbContext.FinancialBooks.AddAsync(financialBook, cancellationToken);
    }
}
