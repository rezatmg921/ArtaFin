using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Features.LeaseAccounting;
using ArtaFin.Domain.Finance;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Infrastructure.Persistence.Repositories;

internal sealed class LeaseContractRepository(ArtaFinDbContext dbContext) : ILeaseContractRepository
{
    private const string UniqueContractNumberIndex = "UX_LeaseContracts_Book_ContractNumber";

    public Task<LeaseContract?> GetByIdAsync(
        Guid financialBookId, Guid leaseContractId, CancellationToken cancellationToken) =>
        dbContext.LeaseContracts.AsNoTracking()
            .SingleOrDefaultAsync(contract =>
                contract.FinancialBookId == financialBookId && contract.Id == leaseContractId,
                cancellationToken);

    public async Task<IReadOnlyList<LeaseContract>> GetByFinancialBookAsync(
        Guid financialBookId, CancellationToken cancellationToken) =>
        await dbContext.LeaseContracts.AsNoTracking()
            .Where(contract => contract.FinancialBookId == financialBookId)
            .OrderByDescending(contract => contract.CommencementDate)
            .ThenBy(contract => contract.ContractNumber)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsByContractNumberAsync(
        Guid financialBookId, string contractNumber, CancellationToken cancellationToken) =>
        dbContext.LeaseContracts.AsNoTracking().AnyAsync(
            contract => contract.FinancialBookId == financialBookId &&
                        contract.ContractNumber == contractNumber,
            cancellationToken);

    public Task AddAsync(LeaseContract contract, CancellationToken cancellationToken) =>
        dbContext.LeaseContracts.AddAsync(contract, cancellationToken).AsTask();

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateContractNumber(exception))
        {
            throw new LeaseContractNumberAlreadyExistsException();
        }
    }

    private static bool IsDuplicateContractNumber(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is not SqlException sqlException)
                continue;

            foreach (SqlError error in sqlException.Errors)
            {
                if (error.Number is 2601 or 2627 &&
                    error.Message.Contains(UniqueContractNumberIndex, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }
}
