using ArtaFin.Domain.Obligations;

namespace ArtaFin.Application.Abstractions.Persistence;

public interface IFinancialObligationRepository
{
    Task<FinancialObligation?> GetByIdAsync(Guid financialBookId, Guid obligationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<FinancialObligation>> ListAsync(
        Guid financialBookId, FinancialObligationDirection? direction,
        FinancialObligationStatus? status, int skip, int take, CancellationToken cancellationToken);
    Task AddAsync(FinancialObligation obligation, CancellationToken cancellationToken);
}
