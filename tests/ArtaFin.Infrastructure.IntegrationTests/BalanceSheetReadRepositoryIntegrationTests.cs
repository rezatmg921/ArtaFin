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

public sealed class BalanceSheetReadRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly DbContextOptions<ArtaFinDbContext> _options;

    public BalanceSheetReadRepositoryIntegrationTests()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var configured = configuration.GetConnectionString("ArtaFin")
            ?? Environment.GetEnvironmentVariable("ARTAFIN_SQLSERVER");
        if (string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException(
                "Configure ConnectionStrings:ArtaFin or ARTAFIN_SQLSERVER for SQL Server integration tests.");

        var builder = new DbConnectionStringBuilder { ConnectionString = configured };
        builder["Initial Catalog"] = $"ArtaFin_BalanceSheet_{Guid.NewGuid():N}";
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
    public async Task GetAsOfAsync_IncludesOnlyPostedEntriesThroughDateAndAllAccountTypes()
    {
        var now = new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
        var asOf = now.AddDays(-1);
        var bookId = Guid.NewGuid();
        var book = FinancialBook.Create(bookId, Guid.NewGuid(), "Balance sheet integration", "USD", now.AddDays(-10));
        var cash = Account.Create(Guid.NewGuid(), bookId, "1000", "Cash", AccountType.Asset, now.AddDays(-10));
        var payable = Account.Create(Guid.NewGuid(), bookId, "2000", "Payables", AccountType.Liability, now.AddDays(-10));
        var capital = Account.Create(Guid.NewGuid(), bookId, "3000", "Capital", AccountType.Equity, now.AddDays(-10));
        var income = Account.Create(Guid.NewGuid(), bookId, "4000", "Sales", AccountType.Income, now.AddDays(-10));
        var expense = Account.Create(Guid.NewGuid(), bookId, "6000", "Supplies", AccountType.Expense, now.AddDays(-10));

        var sale = JournalEntry.Post(Guid.NewGuid(), bookId, asOf, "Sale",
        [
            new JournalLine(cash.Id, PostingSide.Debit, new Money(100m, "USD")),
            new JournalLine(income.Id, PostingSide.Credit, new Money(100m, "USD"))
        ]);
        var payableEntry = JournalEntry.Post(Guid.NewGuid(), bookId, asOf.AddHours(1), "Payable",
        [
            new JournalLine(cash.Id, PostingSide.Debit, new Money(20m, "USD")),
            new JournalLine(payable.Id, PostingSide.Credit, new Money(20m, "USD"))
        ]);
        var supplies = JournalEntry.Post(Guid.NewGuid(), bookId, asOf.AddHours(2), "Supplies",
        [
            new JournalLine(expense.Id, PostingSide.Debit, new Money(25m, "USD")),
            new JournalLine(cash.Id, PostingSide.Credit, new Money(25m, "USD"))
        ]);
        var future = JournalEntry.Post(Guid.NewGuid(), bookId, now.AddDays(1), "Future sale",
        [
            new JournalLine(cash.Id, PostingSide.Debit, new Money(50m, "USD")),
            new JournalLine(income.Id, PostingSide.Credit, new Money(50m, "USD"))
        ]);

        await using (var write = new ArtaFinDbContext(_options))
        {
            write.FinancialBooks.Add(book);
            write.Accounts.AddRange(cash, payable, capital, income, expense);
            write.JournalEntries.AddRange(sale, payableEntry, supplies, future);
            await write.SaveChangesAsync();
        }

        var services = new ServiceCollection();
        var appConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ArtaFin"] = _connectionString
            }).Build();
        services.AddInfrastructure(appConfiguration);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IBalanceSheetReadRepository>();

        var result = await repository.GetAsOfAsync(bookId, asOf.AddHours(3), CancellationToken.None);

        // Assert.Collection verifies that exactly the five expected accounts
        // are returned, in the repository's account-code order.
        Assert.Collection(
            result,
            cashRow =>
            {
                Assert.Equal(cash.Id, cashRow.AccountId);
                Assert.Equal(120m, cashRow.TotalDebits);
                Assert.Equal(25m, cashRow.TotalCredits);
            },
            payableRow =>
            {
                Assert.Equal(payable.Id, payableRow.AccountId);
                Assert.Equal(20m, payableRow.TotalCredits);
            },
            capitalRow => Assert.Equal(capital.Id, capitalRow.AccountId),
            incomeRow =>
            {
                Assert.Equal(income.Id, incomeRow.AccountId);
                Assert.Equal(100m, incomeRow.TotalCredits);
            },
            expenseRow => Assert.Equal(expense.Id, expenseRow.AccountId));
    }

    public async Task DisposeAsync()
    {
        await using var context = new ArtaFinDbContext(_options);
        await context.Database.EnsureDeletedAsync();
    }
}
