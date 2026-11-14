using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Infrastructure.Persistence.Repositories;

internal sealed class AccountRepository(ArtaFinDbContext dbContext)
    : IAccountRepository, IAccountReadRepository
{
    public Task<Account?> GetByIdAsync(
        Guid financialBookId,
        Guid accountId,
        CancellationToken cancellationToken) =>
        dbContext.Accounts.SingleOrDefaultAsync(
            account => account.FinancialBookId == financialBookId &&
                       account.Id == accountId,
            cancellationToken);

    public Task<AccountReadSnapshot?> GetReadSnapshotByIdAsync(
        Guid financialBookId,
        Guid accountId,
        CancellationToken cancellationToken) =>
        dbContext.Accounts
            .Where(account => account.FinancialBookId == financialBookId &&
                              account.Id == accountId)
            .Select(account => new AccountReadSnapshot(
                account,
                EF.Property<byte[]>(account, "RowVersion")))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<AccountReadSnapshot?> GetForUpdateAsync(
        Guid financialBookId,
        Guid accountId,
        byte[]? expectedRowVersion,
        CancellationToken cancellationToken)
    {
        var snapshot = await dbContext.Accounts
            .Where(account => account.FinancialBookId == financialBookId &&
                              account.Id == accountId)
            .Select(account => new AccountReadSnapshot(
                account,
                EF.Property<byte[]>(account, "RowVersion")))
            .SingleOrDefaultAsync(cancellationToken);

        if (snapshot is null)
            return null;

        if (expectedRowVersion is not null)
        {
            dbContext.Entry(snapshot.Account)
                .Property<byte[]>("RowVersion")
                .OriginalValue = expectedRowVersion;
        }

        return snapshot;
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

    public async Task<bool> DeleteIfUnusedAsync(
        Account account,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);

        var isUsed = await dbContext.JournalEntries.AnyAsync(
            entry => entry.FinancialBookId == account.FinancialBookId &&
                     entry.Lines.Any(line => line.AccountId == account.Id),
            cancellationToken);

        if (isUsed)
            return false;

        dbContext.Accounts.Remove(account);
        return true;
    }

    public async Task AddAsync(Account account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        await dbContext.Accounts.AddAsync(account, cancellationToken);
    }

    public async Task<AccountPage> GetPageAsync(
        Guid financialBookId,
        AccountStatus? status,
        string? searchTerm,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.FinancialBookId == financialBookId);

        if (status is not null)
            query = query.Where(account => account.Status == status);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(account =>
                account.Code.Contains(searchTerm) ||
                account.Name.Contains(searchTerm));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(account => account.Code)
            .ThenBy(account => account.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new AccountPage(items, totalCount);
    }
}
