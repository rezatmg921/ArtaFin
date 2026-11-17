using System.Data.Common;
using ArtaFin.Application;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.CashFlowAllocations;
using ArtaFin.Domain.Common;
using ArtaFin.Domain.Finance;
using ArtaFin.Infrastructure;
using ArtaFin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ArtaFin.Infrastructure.IntegrationTests;

public sealed class CashFlowAllocationServiceIntegrationTests : IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly DbContextOptions<ArtaFinDbContext> _options;

    public CashFlowAllocationServiceIntegrationTests()
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
                "SQL Server connection string was not found. Configure ConnectionStrings:ArtaFin " +
                "or set the ARTAFIN_SQLSERVER environment variable.");
        }

        var builder = new DbConnectionStringBuilder { ConnectionString = configuredConnectionString };
        builder["Initial Catalog"] = $"ArtaFin_CashFlowAllocation_{Guid.NewGuid():N}";
        _connectionString = builder.ConnectionString;
        _options = new DbContextOptionsBuilder<ArtaFinDbContext>()
            .UseSqlServer(_connectionString)
            .Options;
    }

    public async Task InitializeAsync()
    {
        await using var context = new ArtaFinDbContext(_options);
        await context.Database.MigrateAsync();
    }

    [Fact]
    public async Task AllocationLifecycle_FinalizesExactAmount_AndKeepsRevisionHistory()
    {
        var now = DateTimeOffset.UtcNow;
        var ownerId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var cashAccountId = Guid.NewGuid();
        var incomeAccountId = Guid.NewGuid();
        var journalEntryId = Guid.NewGuid();

        var book = FinancialBook.Create(bookId, ownerId, "Cash flow allocation test", "USD", now);
        var cash = Account.Create(cashAccountId, bookId, "1000", "Cash", AccountType.Asset, now);
        cash.ConfigureCashFlowClassification(CashFlowAccountType.Cash);
        var income = Account.Create(incomeAccountId, bookId, "4000", "Service income", AccountType.Income, now);
        var entry = JournalEntry.Post(
            journalEntryId,
            bookId,
            now,
            "Customer receipt",
            [
                new JournalLine(cashAccountId, PostingSide.Debit, new Money(100m, "USD")),
                new JournalLine(incomeAccountId, PostingSide.Credit, new Money(100m, "USD"))
            ]);

        await using (var setup = new ArtaFinDbContext(_options))
        {
            setup.FinancialBooks.Add(book);
            setup.Accounts.AddRange(cash, income);
            setup.JournalEntries.Add(entry);
            await setup.SaveChangesAsync();
        }

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ArtaFin"] = _connectionString
            })
            .Build();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddScoped<ICurrentUser>(_ => new TestCurrentUser(ownerId));

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<CashFlowAllocationService>();

        var draft = await service.CreateDraftAsync(
            bookId, journalEntryId, 1, CancellationToken.None);

        Assert.Equal("Draft", draft.Status);
        Assert.Equal(100m, draft.SourceAmount);
        Assert.Equal(100m, draft.RemainingAmount);

        await service.AddAllocationAsync(
            bookId, draft.Id,
            new AddCashFlowAllocationInput(60m, CashFlowActivity.Operating,
                "CUSTOMER_RECEIPTS", 2, "Operating cash receipt"),
            CancellationToken.None);

        var incomplete = await service.GetAsync(bookId, draft.Id, CancellationToken.None);
        Assert.Equal(60m, incomplete.AllocatedAmount);
        Assert.Equal(40m, incomplete.RemainingAmount);

        await Assert.ThrowsAsync<DomainException>(() =>
            service.FinalizeAsync(bookId, draft.Id, CancellationToken.None));

        await service.AddAllocationAsync(
            bookId, draft.Id,
            new AddCashFlowAllocationInput(40m, CashFlowActivity.Operating,
                "CUSTOMER_RECEIPTS", 2, null),
            CancellationToken.None);

        var finalized = await service.FinalizeAsync(
            bookId, draft.Id, CancellationToken.None);

        Assert.Equal("Finalized", finalized.Status);
        Assert.Equal(100m, finalized.AllocatedAmount);
        Assert.Equal(0m, finalized.RemainingAmount);
        Assert.Equal(2, finalized.Allocations.Count);

        await Assert.ThrowsAsync<DomainException>(() =>
            service.AddAllocationAsync(
                bookId, draft.Id,
                new AddCashFlowAllocationInput(1m, CashFlowActivity.Operating,
                    "LATE_EDIT", 2, null),
                CancellationToken.None));

        var revision = await service.CreateRevisionAsync(
            bookId, draft.Id, CancellationToken.None);
        Assert.Equal(2, revision.Version);
        Assert.Equal(draft.Id, revision.SupersedesAllocationSetId);
        Assert.Equal("Draft", revision.Status);

        await service.AddAllocationAsync(
            bookId, revision.Id,
            new AddCashFlowAllocationInput(100m, CashFlowActivity.Operating,
                "CUSTOMER_RECEIPTS", 2, "RECLASSIFIED"),
            CancellationToken.None);

        var revised = await service.FinalizeAsync(
            bookId, revision.Id, CancellationToken.None);

        Assert.Equal("Finalized", revised.Status);
        Assert.Equal(100m, revised.AllocatedAmount);

        var oldVersion = await service.GetAsync(bookId, draft.Id, CancellationToken.None);
        Assert.Equal("Superseded", oldVersion.Status);
        Assert.Equal(2, oldVersion.Allocations.Count);

        var history = await service.GetHistoryAsync(
            bookId, journalEntryId, 1, CancellationToken.None);
        Assert.Equal(2, history.Count);
        Assert.Equal(new[] { "Superseded", "Finalized" },
            history.Select(version => version.Status).ToArray());
    }

    [Fact]
    public async Task CreateDraft_RejectsJournalLinesFromNonCashAccounts()
    {
        var now = DateTimeOffset.UtcNow;
        var ownerId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var bankAccountId = Guid.NewGuid();
        var incomeAccountId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        await using (var setup = new ArtaFinDbContext(_options))
        {
            setup.FinancialBooks.Add(FinancialBook.Create(bookId, ownerId, "Non-cash test", "USD", now));
            setup.Accounts.AddRange(
                Account.Create(bankAccountId, bookId, "1100", "Unclassified bank", AccountType.Asset, now),
                Account.Create(incomeAccountId, bookId, "4000", "Income", AccountType.Income, now));
            setup.JournalEntries.Add(JournalEntry.Post(
                entryId, bookId, now, "Receipt",
                [
                    new JournalLine(bankAccountId, PostingSide.Debit, new Money(25m, "USD")),
                    new JournalLine(incomeAccountId, PostingSide.Credit, new Money(25m, "USD"))
                ]));
            await setup.SaveChangesAsync();
        }

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ArtaFin"] = _connectionString
            })
            .Build();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddScoped<ICurrentUser>(_ => new TestCurrentUser(ownerId));

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<CashFlowAllocationService>();

        await Assert.ThrowsAsync<CashFlowSourceLineInvalidException>(() =>
            service.CreateDraftAsync(bookId, entryId, 1, CancellationToken.None));
    }


    [Fact]
    public async Task AddAllocation_RejectsTransfersBetweenCashAndCashEquivalents()
    {
        var now = DateTimeOffset.UtcNow;
        var ownerId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var cashId = Guid.NewGuid();
        var savingsId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        var cash = Account.Create(cashId, bookId, "1000", "Cash", AccountType.Asset, now);
        cash.ConfigureCashFlowClassification(CashFlowAccountType.Cash);
        var savings = Account.Create(savingsId, bookId, "1010", "Savings", AccountType.Asset, now);
        savings.ConfigureCashFlowClassification(CashFlowAccountType.Cash);

        await using (var setup = new ArtaFinDbContext(_options))
        {
            setup.FinancialBooks.Add(FinancialBook.Create(bookId, ownerId, "Internal transfer test", "USD", now));
            setup.Accounts.AddRange(cash, savings);
            setup.JournalEntries.Add(JournalEntry.Post(
                entryId, bookId, now, "Move cash to savings",
                [
                    new JournalLine(cashId, PostingSide.Debit, new Money(50m, "USD")),
                    new JournalLine(savingsId, PostingSide.Credit, new Money(50m, "USD"))
                ]));
            await setup.SaveChangesAsync();
        }

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ArtaFin"] = _connectionString
            })
            .Build();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddScoped<ICurrentUser>(_ => new TestCurrentUser(ownerId));

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<CashFlowAllocationService>();

        var draft = await service.CreateDraftAsync(bookId, entryId, 1, CancellationToken.None);

        await Assert.ThrowsAsync<CashFlowSourceLineInvalidException>(() =>
            service.AddAllocationAsync(
                bookId, draft.Id,
                new AddCashFlowAllocationInput(50m, CashFlowActivity.Operating,
                    "INTERNAL_TRANSFER", 2, null),
                CancellationToken.None));
    }


    [Fact]
    public async Task ConcurrentAllocations_CannotOverAllocateOneSourceLine()
    {
        var now = DateTimeOffset.UtcNow;
        var ownerId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var cashId = Guid.NewGuid();
        var incomeId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        var cash = Account.Create(cashId, bookId, "1000", "Cash", AccountType.Asset, now);
        cash.ConfigureCashFlowClassification(CashFlowAccountType.Cash);

        await using (var setup = new ArtaFinDbContext(_options))
        {
            setup.FinancialBooks.Add(FinancialBook.Create(bookId, ownerId, "Concurrent allocation test", "USD", now));
            setup.Accounts.AddRange(
                cash,
                Account.Create(incomeId, bookId, "4000", "Income", AccountType.Income, now));
            setup.JournalEntries.Add(JournalEntry.Post(
                entryId, bookId, now, "Receipt",
                [
                    new JournalLine(cashId, PostingSide.Debit, new Money(100m, "USD")),
                    new JournalLine(incomeId, PostingSide.Credit, new Money(100m, "USD"))
                ]));
            await setup.SaveChangesAsync();
        }

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ArtaFin"] = _connectionString
            })
            .Build();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddScoped<ICurrentUser>(_ => new TestCurrentUser(ownerId));

        await using var provider = services.BuildServiceProvider();
        await using var setupScope = provider.CreateAsyncScope();
        var setupService = setupScope.ServiceProvider.GetRequiredService<CashFlowAllocationService>();
        var draft = await setupService.CreateDraftAsync(bookId, entryId, 1, CancellationToken.None);

        async Task<bool> TryAllocateAsync()
        {
            await using var scope = provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<CashFlowAllocationService>();
            try
            {
                await service.AddAllocationAsync(
                    bookId, draft.Id,
                    new AddCashFlowAllocationInput(60m, CashFlowActivity.Operating,
                        "CUSTOMER_RECEIPTS", 2, null),
                    CancellationToken.None);
                return true;
            }
            catch (DomainException)
            {
                return false;
            }
        }

        var outcomes = await Task.WhenAll(TryAllocateAsync(), TryAllocateAsync());

        Assert.Single(outcomes.Where(succeeded => succeeded));
        Assert.Single(outcomes.Where(succeeded => !succeeded));

        var persisted = await setupService.GetAsync(bookId, draft.Id, CancellationToken.None);
        Assert.Equal(60m, persisted.AllocatedAmount);
        Assert.Equal(40m, persisted.RemainingAmount);
    }

    public async Task DisposeAsync()
    {
        await using var context = new ArtaFinDbContext(_options);
        await context.Database.EnsureDeletedAsync();
    }

    private sealed class TestCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }
}
