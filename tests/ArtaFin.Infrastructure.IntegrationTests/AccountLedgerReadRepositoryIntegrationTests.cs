using System.Data.Common;
using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Domain.Finance;
using ArtaFin.Infrastructure;
using ArtaFin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ArtaFin.Infrastructure.IntegrationTests;

public sealed class AccountLedgerReadRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly DbContextOptions<ArtaFinDbContext> _options;

    public AccountLedgerReadRepositoryIntegrationTests()
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
        builder["Initial Catalog"] = $"ArtaFin_AccountLedger_{Guid.NewGuid():N}";
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
    public async Task GetPageAsync_ReturnsNormalBalanceAndCarriesBalanceAcrossPages()
    {
        var now = new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);
        var bookId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var cashId = Guid.NewGuid();
        var incomeId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();

        var book = FinancialBook.Create(bookId, ownerId, "Account ledger persistence", "USD", now);
        var cash = Account.Create(cashId, bookId, "1000", "Cash", AccountType.Asset, now);
        var income = Account.Create(incomeId, bookId, "4000", "Sales", AccountType.Income, now);
        var expense = Account.Create(expenseId, bookId, "6000", "Supplies", AccountType.Expense, now);

        var deposit = JournalEntry.Post(
            Guid.NewGuid(), bookId, now.AddDays(-3), "Cash received",
            [
                new JournalLine(cashId, PostingSide.Debit, new Money(100m, "USD")),
                new JournalLine(incomeId, PostingSide.Credit, new Money(100m, "USD"))
            ]);
        var payment = JournalEntry.Post(
            Guid.NewGuid(), bookId, now.AddDays(-2), "Supplies paid",
            [
                new JournalLine(expenseId, PostingSide.Debit, new Money(30m, "USD")),
                new JournalLine(cashId, PostingSide.Credit, new Money(30m, "USD"))
            ]);
        var transfer = JournalEntry.Post(
            Guid.NewGuid(), bookId, now.AddDays(-1), "Moved to savings",
            [
                new JournalLine(Guid.NewGuid(), PostingSide.Debit, new Money(10m, "USD")),
                new JournalLine(cashId, PostingSide.Credit, new Money(10m, "USD"))
            ]);

        // The destination account in the transfer must exist in the same book.
        var savingsId = transfer.Lines[0].AccountId;
        var savings = Account.Create(savingsId, bookId, "1010", "Savings", AccountType.Asset, now);

        await using (var write = new ArtaFinDbContext(_options))
        {
            write.FinancialBooks.Add(book);
            write.Accounts.AddRange(cash, income, expense, savings);
            write.JournalEntries.AddRange(deposit, payment, transfer);
            await write.SaveChangesAsync();
        }

        var services = new ServiceCollection();
        var appConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ArtaFin"] = _connectionString
            })
            .Build();
        services.AddInfrastructure(appConfiguration);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAccountLedgerReadRepository>();

        var firstPage = await repository.GetPageAsync(
            bookId, cashId, AccountNormalBalance.Debit,
            from: null, to: null, page: 1, pageSize: 2, CancellationToken.None);

        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(0m, firstPage.OpeningBalance);
        Assert.Equal(100m, firstPage.Items[0].BalanceAfter);
        Assert.Equal(70m, firstPage.Items[1].BalanceAfter);
        Assert.Equal("Cash received", firstPage.Items[0].Description);
        Assert.Equal(PostingSide.Credit, firstPage.Items[1].Side);

        var secondPage = await repository.GetPageAsync(
            bookId, cashId, AccountNormalBalance.Debit,
            from: null, to: null, page: 2, pageSize: 2, CancellationToken.None);

        var finalLine = Assert.Single(secondPage.Items);
        Assert.Equal(70m, secondPage.OpeningBalance);
        Assert.Equal(60m, finalLine.BalanceAfter);
        Assert.Equal("Moved to savings", finalLine.Description);

        var dateFiltered = await repository.GetPageAsync(
            bookId, cashId, AccountNormalBalance.Debit,
            from: now.AddDays(-2), to: null, page: 1, pageSize: 10, CancellationToken.None);

        Assert.Equal(2, dateFiltered.TotalCount);
        Assert.Equal(100m, dateFiltered.OpeningBalance);
        Assert.Equal(70m, dateFiltered.Items[0].BalanceAfter);
    }

    public async Task DisposeAsync()
    {
        await using var context = new ArtaFinDbContext(_options);
        await context.Database.EnsureDeletedAsync();
    }
}
