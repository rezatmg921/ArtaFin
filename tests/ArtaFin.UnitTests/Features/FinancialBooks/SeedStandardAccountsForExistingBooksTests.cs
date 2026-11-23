using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Features.FinancialBooks.CreateFinancialBook;
using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests.Features.FinancialBooks;

public sealed class SeedStandardAccountsForExistingBooksTests
{
    [Fact]
    public async Task SeedAsync_IsSafeToRerun_AndPreservesExistingAccounts()
    {
        var firstBookId = Guid.NewGuid();
        var secondBookId = Guid.NewGuid();
        var accountRepository = new FakeAccountRepository();
        var existingAccount = Account.Create(
            Guid.NewGuid(),
            firstBookId,
            "1000",
            "صندوق سفارشی",
            AccountType.Asset,
            DateTimeOffset.UtcNow);
        accountRepository.AddedAccounts.Add(existingAccount);

        var service = new SeedStandardAccountsForExistingBooksService(
            new FakeFinancialBookIdReadRepository(firstBookId, secondBookId),
            accountRepository,
            new FakeUnitOfWork(),
            TimeProvider.System);

        var firstRun = await service.SeedAsync();
        var countAfterFirstRun = accountRepository.AddedAccounts.Count;
        var secondRun = await service.SeedAsync();

        Assert.Equal(2, firstRun.FinancialBooksProcessed);
        Assert.True(firstRun.AccountsCreated > 0);
        Assert.Equal(0, secondRun.AccountsCreated);
        Assert.Equal(countAfterFirstRun, accountRepository.AddedAccounts.Count);
        Assert.Equal("صندوق سفارشی", accountRepository.AddedAccounts
            .Single(account => account.FinancialBookId == firstBookId && account.Code == "1000").Name);
        Assert.Contains(accountRepository.AddedAccounts,
            account => account.FinancialBookId == firstBookId && account.Code == "1100" &&
                       account.Type == AccountType.Asset);
        Assert.Contains(accountRepository.AddedAccounts,
            account => account.FinancialBookId == secondBookId && account.Code == "2000" &&
                       account.Type == AccountType.Liability);
    }

    private sealed class FakeFinancialBookIdReadRepository(params Guid[] ids)
        : IFinancialBookIdReadRepository
    {
        public Task<IReadOnlyList<Guid>> GetAllIdsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>(ids);
    }

    private sealed class FakeAccountRepository : IAccountRepository
    {
        public List<Account> AddedAccounts { get; } = [];

        public Task<Account?> GetByIdAsync(Guid financialBookId, Guid accountId, CancellationToken cancellationToken)
            => Task.FromResult<Account?>(null);

        public Task<AccountReadSnapshot?> GetReadSnapshotByIdAsync(Guid financialBookId, Guid accountId, CancellationToken cancellationToken)
            => Task.FromResult<AccountReadSnapshot?>(null);

        public Task<AccountReadSnapshot?> GetForUpdateAsync(
            Guid financialBookId, Guid accountId, byte[]? expectedRowVersion, CancellationToken cancellationToken)
            => Task.FromResult<AccountReadSnapshot?>(null);

        public Task<bool> ExistsByCodeAsync(Guid financialBookId, string normalizedCode, CancellationToken cancellationToken)
            => Task.FromResult(AddedAccounts.Any(account =>
                account.FinancialBookId == financialBookId && account.Code == normalizedCode));

        public Task<bool> DeleteIfUnusedAsync(Account account, CancellationToken cancellationToken)
            => Task.FromResult(false);

        public Task AddAsync(Account account, CancellationToken cancellationToken)
        {
            AddedAccounts.Add(account);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(1);
    }
}
