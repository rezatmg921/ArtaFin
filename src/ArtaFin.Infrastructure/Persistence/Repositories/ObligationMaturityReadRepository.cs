using ArtaFin.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Infrastructure.Persistence.Repositories;

internal sealed class ObligationMaturityReadRepository(ArtaFinDbContext dbContext)
    : IObligationMaturityReadRepository
{
    public async Task<IReadOnlyList<ObligationMaturitySnapshot>> GetAsOfAsync(
        Guid financialBookId, DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        var asOfDate = DateOnly.FromDateTime(asOf.DateTime);

        var obligations = await dbContext.FinancialObligations
            .AsNoTracking()
            .Include(obligation => obligation.Installments)
            .Where(obligation =>
                obligation.FinancialBookId == financialBookId &&
                obligation.CreatedAt <= asOf &&
                (obligation.CancelledAt == null || obligation.CancelledAt > asOf))
            .OrderBy(obligation => obligation.Id)
            .ToListAsync(cancellationToken);

        if (obligations.Count == 0)
            return [];

        var obligationIds = obligations.Select(obligation => obligation.Id).ToArray();

        var allocations = await (
            from allocation in dbContext.FinancialObligationPaymentAllocations.AsNoTracking()
            join payment in dbContext.FinancialObligationPayments.AsNoTracking()
                on allocation.PaymentId equals payment.Id
            where obligationIds.Contains(allocation.FinancialObligationId) &&
                  payment.FinancialBookId == financialBookId &&
                  payment.PaymentDate <= asOfDate &&
                  payment.CreatedAt <= asOf &&
                  (payment.ReversedAt == null || payment.ReversedAt > asOf)
            select new
            {
                allocation.FinancialObligationId,
                allocation.InstallmentId,
                allocation.Amount
            }).ToListAsync(cancellationToken);

        var paymentsByObligation = allocations
            .GroupBy(allocation => allocation.FinancialObligationId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ObligationInstallmentPayment>)group
                    .Select(allocation => new ObligationInstallmentPayment(
                        allocation.InstallmentId, allocation.Amount))
                    .ToArray());

        return obligations.Select(obligation => new ObligationMaturitySnapshot(
            obligation.Id,
            obligation.ControlAccountId,
            obligation.Direction,
            obligation.Currency,
            obligation.Installments.Select(installment => new ObligationInstallmentTerm(
                installment.Id, installment.DueDate, installment.Amount)).ToArray(),
            paymentsByObligation.GetValueOrDefault(obligation.Id, [])))
            .ToArray();
    }
}
