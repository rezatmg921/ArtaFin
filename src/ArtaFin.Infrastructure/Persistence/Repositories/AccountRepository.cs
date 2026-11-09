using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Infrastructure.Persistence.Repositories;

internal sealed class AccountRepository(ArtaFinDbContext dbContext)
    : IAccountRepository
{
    public Task<Account?> GetByIdAsync(
        Guid financialBookId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        return dbContext.Accounts.SingleOrDefaultAsync(
            account => account.FinancialBookId == financialBookId &&
                       account.Id == accountId,
            cancellationToken);
    }

    public Task<AccountReadSnapshot?> GetReadSnapshotByIdAsync(
        Guid financialBookId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        return dbContext.Accounts
            .Where(account => account.FinancialBookId == financialBookId &&
                              account.Id == accountId)
            .Select(account => new AccountReadSnapshot(
                account,
                EF.Property<byte[]>(account, "RowVersion")))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<bool> ExistsByCodeAsync(
        Guid financialBookId,
        string normalizedCode,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedCode);

        return dbContext.Accounts.AnyAsync(
            account => account.FinancialBookId == financialBookId &&
                       account.Code == normalizedCode,
            cancellationToken);
    }

    public async Task AddAsync(Account account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        await dbContext.Accounts.AddAsync(account, cancellationToken);
    }
}
