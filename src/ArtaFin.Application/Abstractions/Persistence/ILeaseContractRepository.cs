using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Abstractions.Persistence;

public interface ILeaseContractRepository
{
    Task<LeaseContract?> GetByIdAsync(Guid financialBookId, Guid leaseContractId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LeaseContract>> GetByFinancialBookAsync(Guid financialBookId, CancellationToken cancellationToken);
    Task<bool> ExistsByContractNumberAsync(Guid financialBookId, string contractNumber, CancellationToken cancellationToken);
    Task AddAsync(LeaseContract contract, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
