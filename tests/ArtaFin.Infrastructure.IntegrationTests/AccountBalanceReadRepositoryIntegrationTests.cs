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

public sealed class AccountBalanceReadRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly DbContextOptions<ArtaFinDbContext> _options;

    public AccountBalanceReadRepositoryIntegrationTests()
    {
        _connectionString = IntegrationTestDatabase.GetConnectionString();
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
    public async Task GetPageAsync_AggregatesDebitsCreditsAndNormalBalanceForEveryAccount()
    {
        var now = new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);
        var bookId = Guid.NewGuid();
        var book = FinancialBook.Create(bookId, Guid.NewGuid(), "Account balances integration", "USD", now);
        var cash = Account.Create(Guid.NewGuid(), bookId, "1000", "Cash", AccountType.Asset, now);
        var income = Account.Create(Guid.NewGuid(), bookId, "4000", "Sales", AccountType.Income, now);
        var expense = Account.Create(Guid.NewGuid(), bookId, "6000", "Supplies", AccountType.Expense, now);

        var sale = JournalEntry.Post(Guid.NewGuid(), bookId, now.AddDays(-2), "Sale",
        [
            new JournalLine(cash.Id, PostingSide.Debit, new Money(200m, "USD")),
            new JournalLine(income.Id, PostingSide.Credit, new Money(200m, "USD"))
        ]);
        var payment = JournalEntry.Post(Guid.NewGuid(), bookId, now.AddDays(-1), "Supplies",
        [
            new JournalLine(expense.Id, PostingSide.Debit, new Money(50m, "USD")),
            new JournalLine(cash.Id, PostingSide.Credit, new Money(50m, "USD"))
        ]);

        await using (var write = new ArtaFinDbContext(_options))
        {
            write.FinancialBooks.Add(book);
            write.Accounts.AddRange(cash, income, expense);
            write.JournalEntries.AddRange(sale, payment);
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
        var repository = scope.ServiceProvider.GetRequiredService<IAccountBalanceReadRepository>();

        var result = await repository.GetPageAsync(bookId, 1, 10, CancellationToken.None);

        Assert.Equal(3, result.TotalCount);
        var cashBalance = Assert.Single(result.Items, item => item.AccountId == cash.Id);
        Assert.Equal(200m, cashBalance.TotalDebits);
        Assert.Equal(50m, cashBalance.TotalCredits);
        Assert.Equal(150m, cashBalance.Balance);
        Assert.Equal(AccountBalancePolicy.PreventNegative, cashBalance.BalancePolicy);

        var incomeBalance = Assert.Single(result.Items, item => item.AccountId == income.Id);
        Assert.Equal(0m, incomeBalance.TotalDebits);
        Assert.Equal(200m, incomeBalance.TotalCredits);
        Assert.Equal(200m, incomeBalance.Balance);
        Assert.Equal(AccountBalancePolicy.AllowNegative, incomeBalance.BalancePolicy);

        var expenseBalance = Assert.Single(result.Items, item => item.AccountId == expense.Id);
        Assert.Equal(50m, expenseBalance.TotalDebits);
        Assert.Equal(0m, expenseBalance.TotalCredits);
        Assert.Equal(50m, expenseBalance.Balance);
    }

    public async Task DisposeAsync()
    {
        await using var context = new ArtaFinDbContext(_options);
        await context.Database.EnsureDeletedAsync();
    }
}
