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

public sealed class IncomeStatementReadRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly DbContextOptions<ArtaFinDbContext> _options;

    public IncomeStatementReadRepositoryIntegrationTests()
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
    public async Task GetPeriodAsync_UsesHalfOpenPeriodAndOnlyIncomeAndExpenseAccounts()
    {
        var now = new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
        var from = now.AddDays(-3);
        var to = now.AddDays(-1);
        var bookId = Guid.NewGuid();
        var book = FinancialBook.Create(bookId, Guid.NewGuid(), "Income statement integration", "USD", now.AddDays(-10));
        var cash = Account.Create(Guid.NewGuid(), bookId, "1000", "Cash", AccountType.Asset, now.AddDays(-10));
        var income = Account.Create(Guid.NewGuid(), bookId, "4000", "Sales", AccountType.Income, now.AddDays(-10));
        var expense = Account.Create(Guid.NewGuid(), bookId, "6000", "Supplies", AccountType.Expense, now.AddDays(-10));
        var noActivityExpense = Account.Create(Guid.NewGuid(), bookId, "6100", "Rent", AccountType.Expense, now.AddDays(-10));

        var sale = JournalEntry.Post(Guid.NewGuid(), bookId, from, "Sale",
        [
            new JournalLine(cash.Id, PostingSide.Debit, new Money(100m, "USD")),
            new JournalLine(income.Id, PostingSide.Credit, new Money(100m, "USD"))
        ]);
        var supplies = JournalEntry.Post(Guid.NewGuid(), bookId, from.AddHours(1), "Supplies",
        [
            new JournalLine(expense.Id, PostingSide.Debit, new Money(25m, "USD")),
            new JournalLine(cash.Id, PostingSide.Credit, new Money(25m, "USD"))
        ]);
        var atExclusiveEnd = JournalEntry.Post(Guid.NewGuid(), bookId, to, "Late sale",
        [
            new JournalLine(cash.Id, PostingSide.Debit, new Money(50m, "USD")),
            new JournalLine(income.Id, PostingSide.Credit, new Money(50m, "USD"))
        ]);

        await using (var write = new ArtaFinDbContext(_options))
        {
            write.FinancialBooks.Add(book);
            write.Accounts.AddRange(cash, income, expense, noActivityExpense);
            write.JournalEntries.AddRange(sale, supplies, atExclusiveEnd);
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
        var repository = scope.ServiceProvider.GetRequiredService<IIncomeStatementReadRepository>();

        var result = await repository.GetPeriodAsync(bookId, from, to, CancellationToken.None);

        Assert.Equal(3, result.Count);
        var incomeRow = Assert.Single(result, item => item.AccountId == income.Id);
        Assert.Equal(0m, incomeRow.TotalDebits);
        Assert.Equal(100m, incomeRow.TotalCredits);

        var expenseRow = Assert.Single(result, item => item.AccountId == expense.Id);
        Assert.Equal(25m, expenseRow.TotalDebits);
        Assert.Equal(0m, expenseRow.TotalCredits);

        var zeroRow = Assert.Single(result, item => item.AccountId == noActivityExpense.Id);
        Assert.Equal(0m, zeroRow.TotalDebits);
        Assert.Equal(0m, zeroRow.TotalCredits);
        Assert.DoesNotContain(result, item => item.AccountId == cash.Id);
    }

    public async Task DisposeAsync()
    {
        await using var context = new ArtaFinDbContext(_options);
        await context.Database.EnsureDeletedAsync();
    }
}
