using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Abstractions.Persistence;

/// <summary>Write operations for creating financial-book aggregates.</summary>
public interface IFinancialBookWriter
{
    Task AddAsync(FinancialBook financialBook, CancellationToken cancellationToken);
}
