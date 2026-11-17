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

public sealed class TrialBalanceReadRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly DbContextOptions<ArtaFinDbContext> _options;

    public TrialBalanceReadRepositoryIntegrationTests()
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
        builder["Initial Catalog"] = $"ArtaFin_TrialBalance_{Guid.NewGuid():N}";
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
    public async Task GetAsOfAsync_UsesOnlyEntriesPostedOnOrBeforeCutoff_AndBalancesTrialBalance()
    {
        var now = new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
        var cutoff = now.AddDays(-1);
        var bookId = Guid.NewGuid();
        var book = FinancialBook.Create(bookId, Guid.NewGuid(), "Trial balance integration", "USD", now.AddDays(-10));
        var cash = Account.Create(Guid.NewGuid(), bookId, "1000", "Cash", AccountType.Asset, now.AddDays(-10));
        var income = Account.Create(Guid.NewGuid(), bookId, "4000", "Sales", AccountType.Income, now.AddDays(-10));
        var expense = Account.Create(Guid.NewGuid(), bookId, "6000", "Supplies", AccountType.Expense, now.AddDays(-10));

        var sale = JournalEntry.Post(Guid.NewGuid(), bookId, now.AddDays(-2), "Sale",
        [
            new JournalLine(cash.Id, PostingSide.Debit, new Money(100m, "USD")),
            new JournalLine(income.Id, PostingSide.Credit, new Money(100m, "USD"))
        ]);
        var laterExpense = JournalEntry.Post(Guid.NewGuid(), bookId, now, "Supplies",
        [
            new JournalLine(expense.Id, PostingSide.Debit, new Money(20m, "USD")),
            new JournalLine(cash.Id, PostingSide.Credit, new Money(20m, "USD"))
        ]);

        await using (var write = new ArtaFinDbContext(_options))
        {
            write.FinancialBooks.Add(book);
            write.Accounts.AddRange(cash, income, expense);
            write.JournalEntries.AddRange(sale, laterExpense);
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
        var repository = scope.ServiceProvider.GetRequiredService<ITrialBalanceReadRepository>();

        var result = await repository.GetAsOfAsync(bookId, cutoff, CancellationToken.None);

        Assert.Equal(3, result.Count);
        var cashRow = Assert.Single(result, item => item.AccountId == cash.Id);
        Assert.Equal(100m, cashRow.TotalDebits);
        Assert.Equal(0m, cashRow.TotalCredits);

        var incomeRow = Assert.Single(result, item => item.AccountId == income.Id);
        Assert.Equal(0m, incomeRow.TotalDebits);
        Assert.Equal(100m, incomeRow.TotalCredits);

        var expenseRow = Assert.Single(result, item => item.AccountId == expense.Id);
        Assert.Equal(0m, expenseRow.TotalDebits);
        Assert.Equal(0m, expenseRow.TotalCredits);
    }

    public async Task DisposeAsync()
    {
        await using var context = new ArtaFinDbContext(_options);
        await context.Database.EnsureDeletedAsync();
    }
}
