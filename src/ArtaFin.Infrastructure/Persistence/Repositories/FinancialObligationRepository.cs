using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Domain.Obligations;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Infrastructure.Persistence.Repositories;

internal sealed class FinancialObligationRepository(ArtaFinDbContext dbContext) : IFinancialObligationRepository
{
    public Task<FinancialObligation?> GetByIdAsync(
        Guid financialBookId, Guid obligationId, CancellationToken cancellationToken) =>
        dbContext.FinancialObligations.SingleOrDefaultAsync(
            x => x.FinancialBookId == financialBookId && x.Id == obligationId,
            cancellationToken);

    public async Task<IReadOnlyList<FinancialObligation>> ListAsync(
        Guid financialBookId, FinancialObligationDirection? direction,
        FinancialObligationStatus? status, int skip, int take, CancellationToken cancellationToken)
    {
        var query = dbContext.FinancialObligations.AsNoTracking()
            .Where(x => x.FinancialBookId == financialBookId);
        if (direction.HasValue) query = query.Where(x => x.Direction == direction.Value);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        return await query.OrderBy(x => x.DueDate).ThenBy(x => x.Id)
            .Skip(skip).Take(take).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(FinancialObligation obligation, CancellationToken cancellationToken) =>
        await dbContext.FinancialObligations.AddAsync(obligation, cancellationToken);
}
