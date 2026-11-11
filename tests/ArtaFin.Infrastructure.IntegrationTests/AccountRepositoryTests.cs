using System.Data.Common;
using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Features.Accounts.CreateAccount;
using ArtaFin.Application.Features.Accounts.DeleteAccount;
using ArtaFin.Domain.Finance;
using ArtaFin.Infrastructure;
using ArtaFin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ArtaFin.Infrastructure.IntegrationTests;

public sealed class AccountRepositoryTests : IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly DbContextOptions<ArtaFinDbContext> _options;

    public AccountRepositoryTests()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var configuredConnectionString =
            configuration.GetConnectionString("ArtaFin")
            ?? Environment.GetEnvironmentVariable("ARTAFIN_SQLSERVER");

        if (string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            throw new InvalidOperationException(
                "SQL Server connection string was not found. Add ConnectionStrings:ArtaFin " +
                "to tests/ArtaFin.Infrastructure.IntegrationTests/appsettings.Local.json, " +
                "or set the ConnectionStrings__ArtaFin environment variable.");
        }

        var builder = new DbConnectionStringBuilder { ConnectionString = configuredConnectionString };
        builder["Initial Catalog"] = $"ArtaFin_AccountRepository_{Guid.NewGuid():N}";
        _connectionString = builder.ConnectionString;
        _options = new DbContextOptionsBuilder<ArtaFinDbContext>().UseSqlServer(_connectionString).Options;
    }

    public async Task InitializeAsync()
    {
        await using var context = new ArtaFinDbContext(_options);
        await context.Database.MigrateAsync();
    }

    [Fact]
    public async Task Repository_ScopesReadsToFinancialBook_AndCommitsAddedAccounts()
    {
        var now = DateTimeOffset.UtcNow;
        var bookAId = Guid.NewGuid();
        var bookBId = Guid.NewGuid();
        var accountAId = Guid.NewGuid();

        await using (var setup = new ArtaFinDbContext(_options))
        {
            setup.FinancialBooks.AddRange(
                FinancialBook.Create(bookAId, Guid.NewGuid(), "Book A", "USD", now),
                FinancialBook.Create(bookBId, Guid.NewGuid(), "Book B", "USD", now));
            setup.Accounts.Add(Account.Create(
                accountAId, bookAId, "1000", "Cash", AccountType.Asset, now));
            await setup.SaveChangesAsync();
        }

        var servicesConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ArtaFin"] = _connectionString
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(servicesConfiguration);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var repository = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var account = await repository.GetByIdAsync(bookAId, accountAId, CancellationToken.None);
        var crossBookLookup = await repository.GetByIdAsync(bookBId, accountAId, CancellationToken.None);

        Assert.NotNull(account);
        Assert.Equal("1000", account.Code);
        Assert.Null(crossBookLookup);

        var accountSnapshot = await repository.GetReadSnapshotByIdAsync(
            bookAId, accountAId, CancellationToken.None);
        Assert.NotNull(accountSnapshot);
        Assert.Equal(accountAId, accountSnapshot.Account.Id);
        Assert.Equal(8, accountSnapshot.RowVersion.Length);
        Assert.True(await repository.ExistsByCodeAsync(bookAId, "1000", CancellationToken.None));
        Assert.False(await repository.ExistsByCodeAsync(bookBId, "1000", CancellationToken.None));

        var addedAccount = Account.Create(
            Guid.NewGuid(), bookBId, "2000", "Bank", AccountType.Asset, now);
        await repository.AddAsync(addedAccount, CancellationToken.None);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        var persisted = await repository.GetByIdAsync(
            bookBId, addedAccount.Id, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal("2000", persisted.Code);

        // The pre-check can be bypassed by concurrent requests. The SQL Server
        // unique index must still reject the duplicate and expose an application error.
        var duplicateAccount = Account.Create(
            Guid.NewGuid(), bookAId, "1000", "Duplicate Cash", AccountType.Asset, now);
        await repository.AddAsync(duplicateAccount, CancellationToken.None);

        await Assert.ThrowsAsync<AccountCodeAlreadyExistsException>(() =>
            unitOfWork.SaveChangesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DeleteIfUnused_RejectsAccountsWithJournalHistory_AndDeletesUnusedAccounts()
    {
        var now = DateTimeOffset.UtcNow;
        var bookId = Guid.NewGuid();
        var usedAccountId = Guid.NewGuid();
        var offsetAccountId = Guid.NewGuid();
        var unusedAccountId = Guid.NewGuid();

        await using (var setup = new ArtaFinDbContext(_options))
        {
            setup.FinancialBooks.Add(FinancialBook.Create(
                bookId, Guid.NewGuid(), "Account deletion test", "USD", now));
            setup.Accounts.AddRange(
                Account.Create(usedAccountId, bookId, "1000", "Cash", AccountType.Asset, now),
                Account.Create(offsetAccountId, bookId, "4000", "Income", AccountType.Income, now),
                Account.Create(unusedAccountId, bookId, "1100", "Unused", AccountType.Asset, now));
            setup.JournalEntries.Add(JournalEntry.Post(
                Guid.NewGuid(), bookId, now, "Cash receipt",
                [
                    new JournalLine(usedAccountId, PostingSide.Debit, new Money(50m, "USD")),
                    new JournalLine(offsetAccountId, PostingSide.Credit, new Money(50m, "USD"))
                ]));
            await setup.SaveChangesAsync();
        }

        var servicesConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ArtaFin"] = _connectionString
            })
            .Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(servicesConfiguration);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var repository = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var usedAccount = await repository.GetByIdAsync(bookId, usedAccountId, CancellationToken.None);
        Assert.NotNull(usedAccount);
        var deletedUsedAccount = await repository.DeleteIfUnusedAsync(usedAccount, CancellationToken.None);
        Assert.False(deletedUsedAccount);

        // The database FK is the final guard if a journal line appears after the pre-check.
        await using (var directDelete = new ArtaFinDbContext(_options))
        {
            var accountWithHistory = await directDelete.Accounts
                .SingleAsync(x => x.Id == usedAccountId);

            directDelete.Accounts.Remove(accountWithHistory);

            await Assert.ThrowsAsync<AccountHasJournalEntriesException>(
                () => directDelete.SaveChangesAsync());
        }

        var unusedAccount = await repository.GetByIdAsync(bookId, unusedAccountId, CancellationToken.None);
        Assert.NotNull(unusedAccount);
        var deletedUnusedAccount = await repository.DeleteIfUnusedAsync(unusedAccount, CancellationToken.None);
        Assert.True(deletedUnusedAccount);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        await using var verification = new ArtaFinDbContext(_options);
        Assert.NotNull(await verification.Accounts.SingleOrDefaultAsync(x => x.Id == usedAccountId));
        Assert.Null(await verification.Accounts.SingleOrDefaultAsync(x => x.Id == unusedAccountId));
    }

    [Fact]
    public async Task ConcurrentUpdates_RejectStaleAccountVersion()
    {
        var now = DateTimeOffset.UtcNow;
        var bookId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        await using (var setup = new ArtaFinDbContext(_options))
        {
            setup.FinancialBooks.Add(FinancialBook.Create(
                bookId, Guid.NewGuid(), "Concurrency Test Book", "USD", now));
            setup.Accounts.Add(Account.Create(
                accountId, bookId, "1000", "Original Cash", AccountType.Asset, now));
            await setup.SaveChangesAsync();
        }

        // Two independent contexts read the same original RowVersion.
        await using var contextA = new ArtaFinDbContext(_options);
        await using var contextB = new ArtaFinDbContext(_options);

        var accountA = await contextA.Accounts.SingleAsync(x => x.Id == accountId);
        var accountB = await contextB.Accounts.SingleAsync(x => x.Id == accountId);

        accountA.Rename("Cash - Updated By A");
        accountB.Rename("Cash - Updated By B");

        // The first update succeeds and advances the database RowVersion.
        await contextA.SaveChangesAsync();

        // The second update uses the stale RowVersion and must be rejected.
        await Assert.ThrowsAsync<PersistenceConcurrencyException>(
            () => contextB.SaveChangesAsync());

        await using var verification = new ArtaFinDbContext(_options);
        var persistedAccount = await verification.Accounts
            .AsNoTracking()
            .SingleAsync(x => x.Id == accountId);

        Assert.Equal("Cash - Updated By A", persistedAccount.Name);
    }

    [Fact]
    public async Task DeactivateAndReactivate_PersistStatusAndAdvanceRowVersion()
    {
        var now = DateTimeOffset.UtcNow;
        var bookId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        await using (var setup = new ArtaFinDbContext(_options))
        {
            setup.FinancialBooks.Add(FinancialBook.Create(
                bookId, Guid.NewGuid(), "Account status test", "USD", now));
            setup.Accounts.Add(Account.Create(
                accountId, bookId, "1000", "Cash", AccountType.Asset, now));
            await setup.SaveChangesAsync();
        }

        byte[] initialRowVersion;
        await using (var deactivateContext = new ArtaFinDbContext(_options))
        {
            var account = await deactivateContext.Accounts
                .SingleAsync(x => x.Id == accountId);
            initialRowVersion = await deactivateContext.Accounts
                .Where(x => x.Id == accountId)
                .Select(x => EF.Property<byte[]>(x, "RowVersion"))
                .SingleAsync();

            account.Deactivate(now.AddMinutes(1));
            await deactivateContext.SaveChangesAsync();
        }

        byte[] inactiveRowVersion;
        await using (var verifyInactive = new ArtaFinDbContext(_options))
        {
            var inactiveAccount = await verifyInactive.Accounts
                .AsNoTracking()
                .SingleAsync(x => x.Id == accountId);
            inactiveRowVersion = await verifyInactive.Accounts
                .Where(x => x.Id == accountId)
                .Select(x => EF.Property<byte[]>(x, "RowVersion"))
                .SingleAsync();

            Assert.Equal(AccountStatus.Inactive, inactiveAccount.Status);
            Assert.Equal(now.AddMinutes(1), inactiveAccount.DeactivatedAt);
        }

        Assert.NotEqual(initialRowVersion, inactiveRowVersion);

        await using (var reactivateContext = new ArtaFinDbContext(_options))
        {
            var account = await reactivateContext.Accounts
                .SingleAsync(x => x.Id == accountId);

            account.Reactivate(now.AddMinutes(2));
            await reactivateContext.SaveChangesAsync();
        }

        await using (var verifyActive = new ArtaFinDbContext(_options))
        {
            var activeAccount = await verifyActive.Accounts
                .AsNoTracking()
                .SingleAsync(x => x.Id == accountId);
            var activeRowVersion = await verifyActive.Accounts
                .Where(x => x.Id == accountId)
                .Select(x => EF.Property<byte[]>(x, "RowVersion"))
                .SingleAsync();

            Assert.Equal(AccountStatus.Active, activeAccount.Status);
            Assert.Null(activeAccount.DeactivatedAt);
            Assert.NotEqual(inactiveRowVersion, activeRowVersion);
        }
    }


    [Fact]
    public async Task ListAccounts_FiltersByStatusAndSearch_AndAppliesStablePaging()
    {
        var now = DateTimeOffset.UtcNow;
        var bookId = Guid.NewGuid();

        await using (var setup = new ArtaFinDbContext(_options))
        {
            setup.FinancialBooks.Add(FinancialBook.Create(
                bookId, Guid.NewGuid(), "Account listing test", "USD", now));
            var cash = Account.Create(
                Guid.NewGuid(), bookId, "1000", "Cash", AccountType.Asset, now);
            var bank = Account.Create(
                Guid.NewGuid(), bookId, "1100", "Bank", AccountType.Asset, now);
            var revenue = Account.Create(
                Guid.NewGuid(), bookId, "4000", "Sales Revenue", AccountType.Income, now);
            revenue.Deactivate(now.AddMinutes(1));
            setup.Accounts.AddRange(cash, bank, revenue);
            await setup.SaveChangesAsync();
        }

        var servicesConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ArtaFin"] = _connectionString
            })
            .Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(servicesConfiguration);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var repository = scope.ServiceProvider.GetRequiredService<IAccountReadRepository>();

        var firstPage = await repository.GetPageAsync(
            bookId, AccountStatus.Active, null, 1, 1, CancellationToken.None);
        var secondPage = await repository.GetPageAsync(
            bookId, AccountStatus.Active, null, 2, 1, CancellationToken.None);
        var inactiveSearch = await repository.GetPageAsync(
            bookId, AccountStatus.Inactive, "revenue", 1, 10, CancellationToken.None);

        Assert.Equal(2, firstPage.TotalCount);
        Assert.Single(firstPage.Items);
        Assert.Equal("1000", firstPage.Items[0].Code);
        Assert.Single(secondPage.Items);
        Assert.Equal("1100", secondPage.Items[0].Code);
        Assert.Equal(1, inactiveSearch.TotalCount);
        Assert.Equal("4000", Assert.Single(inactiveSearch.Items).Code);
    }

    public async Task DisposeAsync()
    {
        await using var context = new ArtaFinDbContext(_options);
        await context.Database.EnsureDeletedAsync();
    }
}
