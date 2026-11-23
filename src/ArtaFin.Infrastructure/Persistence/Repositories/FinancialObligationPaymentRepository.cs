using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Domain.Obligations;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Infrastructure.Persistence.Repositories;

internal sealed class FinancialObligationPaymentRepository(ArtaFinDbContext dbContext)
    : IFinancialObligationPaymentRepository
{
    public async Task<FinancialObligation?> GetObligationForPaymentAsync(
        Guid financialBookId, Guid obligationId, CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Obligation payment reads require an active transaction.");

        var lockedId = await dbContext.FinancialObligations
            .FromSqlInterpolated($@"SELECT * FROM [FinancialObligations] WITH (UPDLOCK, HOLDLOCK)
                WHERE [FinancialBookId] = {financialBookId} AND [Id] = {obligationId}")
            .AsNoTracking()
            .Select(x => x.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (lockedId == Guid.Empty)
            return null;

        return await dbContext.FinancialObligations
            .Include(x => x.Installments)
            .SingleOrDefaultAsync(x => x.FinancialBookId == financialBookId && x.Id == obligationId, cancellationToken);
    }

    public Task<FinancialObligationPayment?> GetByIdAsync(
        Guid financialBookId, Guid paymentId, CancellationToken cancellationToken) =>
        dbContext.FinancialObligationPayments.AsNoTracking()
            .Include(x => x.Allocations)
            .SingleOrDefaultAsync(x => x.FinancialBookId == financialBookId && x.Id == paymentId, cancellationToken);

    public Task<FinancialObligationPayment?> GetByIdempotencyKeyAsync(
        Guid financialBookId, Guid userId, string idempotencyKey, CancellationToken cancellationToken) =>
        dbContext.FinancialObligationPayments.AsNoTracking()
            .Include(x => x.Allocations)
            .SingleOrDefaultAsync(x => x.FinancialBookId == financialBookId &&
                x.CreatedBy == userId && x.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task<IReadOnlyList<FinancialObligationPayment>> ListByObligationAsync(
        Guid financialBookId, Guid obligationId, int skip, int take, CancellationToken cancellationToken) =>
        await dbContext.FinancialObligationPayments.AsNoTracking()
            .Include(x => x.Allocations)
            .Where(x => x.FinancialBookId == financialBookId && x.FinancialObligationId == obligationId)
            .OrderByDescending(x => x.PaymentDate).ThenByDescending(x => x.Id)
            .Skip(skip).Take(take).ToListAsync(cancellationToken);

    public async Task<decimal> GetAllocatedAmountForObligationAsync(
        Guid financialObligationId, CancellationToken cancellationToken) =>
        await dbContext.FinancialObligationPaymentAllocations.AsNoTracking()
            .Where(x => x.FinancialObligationId == financialObligationId)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetAllocatedAmountsByInstallmentAsync(
        Guid financialObligationId, CancellationToken cancellationToken)
    {
        var values = await dbContext.FinancialObligationPaymentAllocations.AsNoTracking()
            .Where(x => x.FinancialObligationId == financialObligationId)
            .GroupBy(x => x.InstallmentId)
            .Select(group => new { InstallmentId = group.Key, Amount = group.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);
        return values.ToDictionary(x => x.InstallmentId, x => x.Amount);
    }

    public async Task AddAsync(FinancialObligationPayment payment, CancellationToken cancellationToken) =>
        await dbContext.FinancialObligationPayments.AddAsync(payment, cancellationToken);
}