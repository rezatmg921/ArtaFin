using ArtaFin.Domain.Obligations;

namespace ArtaFin.Application.Abstractions.Persistence;

public interface IFinancialObligationPaymentRepository
{
    Task<FinancialObligation?> GetObligationForPaymentAsync(Guid financialBookId, Guid obligationId, CancellationToken cancellationToken);
    Task<FinancialObligationPayment?> GetByIdAsync(Guid financialBookId, Guid paymentId, CancellationToken cancellationToken);
    Task<FinancialObligationPayment?> GetForReversalAsync(Guid financialBookId, Guid obligationId, Guid paymentId, CancellationToken cancellationToken);
    Task<FinancialObligationPayment?> GetByIdempotencyKeyAsync(Guid financialBookId, Guid userId, string idempotencyKey, CancellationToken cancellationToken);
    Task<IReadOnlyList<FinancialObligationPayment>> ListByObligationAsync(Guid financialBookId, Guid obligationId, int skip, int take, CancellationToken cancellationToken);
    Task<decimal> GetAllocatedAmountForObligationAsync(Guid financialObligationId, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, decimal>> GetAllocatedAmountsByInstallmentAsync(Guid financialObligationId, CancellationToken cancellationToken);
    Task<decimal> GetAllocatedAmountExcludingPaymentAsync(Guid financialObligationId, Guid excludedPaymentId, CancellationToken cancellationToken);
    Task AddAsync(FinancialObligationPayment payment, CancellationToken cancellationToken);
}