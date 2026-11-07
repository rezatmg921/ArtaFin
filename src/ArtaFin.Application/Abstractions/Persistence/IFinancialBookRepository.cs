using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Abstractions.Persistence;

/// <summary>
/// Persistence port for loading a FinancialBook aggregate.
/// </summary>
public interface IFinancialBookRepository
{
    Task<FinancialBook?> GetByIdAsync(
        Guid financialBookId,
        CancellationToken cancellationToken);
}
