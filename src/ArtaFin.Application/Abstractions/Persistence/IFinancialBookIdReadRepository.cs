namespace ArtaFin.Application.Abstractions.Persistence;

/// <summary>
/// Reads financial-book identifiers for controlled maintenance operations.
/// </summary>
public interface IFinancialBookIdReadRepository
{
    Task<IReadOnlyList<Guid>> GetAllIdsAsync(CancellationToken cancellationToken);
}
