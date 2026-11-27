using ArtaFin.Application;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Reports.GetBalanceSheet;
using ArtaFin.Domain.Finance;
using ArtaFin.Infrastructure;
using ArtaFin.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ArtaFin.Infrastructure.IntegrationTests;

public sealed class BalanceSheetQueryIntegrationTests : IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly DbContextOptions<ArtaFinDbContext> _options;

    public BalanceSheetQueryIntegrationTests()
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
    public async Task Handle_CalculatesBalancedStatementFromSqlServerAndExcludesFuturePostings()
    {
        var asOf = new DateTimeOffset(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);
        var ownerId = Guid.NewGuid();
        var bookId = Guid.NewGuid();

        var book = FinancialBook.Create(
            bookId, ownerId, "Balance sheet query integration", "USD", asOf.AddDays(-30));
        var asset = Account.Create(
            Guid.NewGuid(), bookId, "1000", "Equipment", AccountType.Asset, asOf.AddDays(-30));
        var contraAsset = Account.Create(
            Guid.NewGuid(), bookId, "1090", "Accumulated depreciation", AccountType.Asset, asOf.AddDays(-30));
        var liability = Account.Create(
            Guid.NewGuid(), bookId, "2000", "Payables", AccountType.Liability, asOf.AddDays(-30));
        var equity = Account.Create(
            Guid.NewGuid(), bookId, "3000", "Retained earnings", AccountType.Equity, asOf.AddDays(-30));
        var income = Account.Create(
            Guid.NewGuid(), bookId, "4000", "Revenue", AccountType.Income, asOf.AddDays(-30));
        var expense = Account.Create(
            Guid.NewGuid(), bookId, "6000", "Operating expense", AccountType.Expense, asOf.AddDays(-30));

        var posted = JournalEntry.Post(
            Guid.NewGuid(), bookId, asOf, "Opening balances and current-period activity",
            [
                new JournalLine(asset.Id, PostingSide.Debit, new Money(175m, "USD")),
                new JournalLine(contraAsset.Id, PostingSide.Credit, new Money(20m, "USD")),
                new JournalLine(liability.Id, PostingSide.Debit, new Money(5m, "USD")),
                new JournalLine(liability.Id, PostingSide.Credit, new Money(40m, "USD")),
                new JournalLine(equity.Id, PostingSide.Debit, new Money(10m, "USD")),
                new JournalLine(equity.Id, PostingSide.Credit, new Money(55m, "USD")),
                new JournalLine(income.Id, PostingSide.Credit, new Money(100m, "USD")),
                new JournalLine(expense.Id, PostingSide.Debit, new Money(25m, "USD"))
            ]);

        var future = JournalEntry.Post(
            Guid.NewGuid(), bookId, asOf.AddDays(1), "Future revenue",
            [
                new JournalLine(asset.Id, PostingSide.Debit, new Money(50m, "USD")),
                new JournalLine(income.Id, PostingSide.Credit, new Money(50m, "USD"))
            ]);

        await using (var write = new ArtaFinDbContext(_options))
        {
            write.FinancialBooks.Add(book);
            write.Accounts.AddRange(asset, contraAsset, liability, equity, income, expense);
            write.JournalEntries.AddRange(posted, future);
            await write.SaveChangesAsync();
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ArtaFin"] = _connectionString
            })
            .Build();
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddScoped<ICurrentUser>(_ => new TestCurrentUser(ownerId));

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var result = await sender.Send(
            new GetBalanceSheetQuery(bookId, asOf),
            CancellationToken.None);

        Assert.Equal(asOf, result.AsOf);
        Assert.Equal("USD", result.Currency);

        Assert.Equal(155m, result.TotalAssets);
        Assert.Equal(35m, result.TotalLiabilities);
        Assert.Equal(45m, result.Equity.Sum(account => account.Amount));
        Assert.Equal(75m, result.UnclosedNetIncome);
        Assert.Equal(120m, result.TotalEquity);
        Assert.Equal(155m, result.TotalLiabilitiesAndEquity);
        Assert.Equal(0m, result.Difference);

        Assert.Contains(result.Assets, account =>
            account.AccountId == asset.Id && account.Amount == 175m);
        Assert.Contains(result.Assets, account =>
            account.AccountId == contraAsset.Id && account.Amount == -20m);
        Assert.DoesNotContain(result.Assets, account => account.Amount == 225m);
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
