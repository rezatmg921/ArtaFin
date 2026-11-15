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

public sealed class JournalEntryReadRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly DbContextOptions<ArtaFinDbContext> _options;

    public JournalEntryReadRepositoryIntegrationTests()
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
        builder["Initial Catalog"] = $"ArtaFin_JournalRead_{Guid.NewGuid():N}";
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
    public async Task GetPageAsync_FiltersByBookDateSearchAndReversalAndPaginates()
    {
        var now = new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
        var bookId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var cashId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var book = FinancialBook.Create(bookId, ownerId, "Journal read test", "USD", now);
        var cash = Account.Create(cashId, bookId, "1000", "Cash", AccountType.Asset, now);
        var expense = Account.Create(expenseId, bookId, "6000", "Office expense", AccountType.Expense, now);
        var original = JournalEntry.Post(
            Guid.NewGuid(),
            bookId,
            now.AddDays(-2),
            "Office supplies",
            [
                new JournalLine(expenseId, PostingSide.Debit, new Money(50m, "USD")),
                new JournalLine(cashId, PostingSide.Credit, new Money(50m, "USD"))
            ]);
        var reversal = JournalEntry.Reverse(
            Guid.NewGuid(),
            original,
            now.AddDays(-1),
            "Office supplies reversal");
        var unrelated = JournalEntry.Post(
            Guid.NewGuid(),
            bookId,
            now,
            "Monthly rent",
            [
                new JournalLine(expenseId, PostingSide.Debit, new Money(200m, "USD")),
                new JournalLine(cashId, PostingSide.Credit, new Money(200m, "USD"))
            ]);

        await using (var write = new ArtaFinDbContext(_options))
        {
            write.FinancialBooks.Add(book);
            write.Accounts.AddRange(cash, expense);
            write.JournalEntries.AddRange(original, reversal, unrelated);
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
        var repository = scope.ServiceProvider.GetRequiredService<IJournalEntryReadRepository>();

        var reversals = await repository.GetPageAsync(
            bookId,
            now.AddDays(-2),
            now.AddDays(-1),
            "Office",
            isReversal: true,
            page: 1,
            pageSize: 10,
            CancellationToken.None);

        Assert.Equal(1, reversals.TotalCount);
        var reversalItem = Assert.Single(reversals.Items);
        Assert.Equal(reversal.Id, reversalItem.Id);
        Assert.Equal(original.Id, reversalItem.ReversesJournalEntryId);
        Assert.Equal("Office supplies reversal", reversalItem.Description);
        Assert.Equal(2, reversalItem.LineCount);

        var originals = await repository.GetPageAsync(
            bookId,
            null,
            now.AddDays(-2),
            "Office",
            isReversal: false,
            page: 1,
            pageSize: 1,
            CancellationToken.None);

        Assert.Equal(1, originals.TotalCount);
        Assert.Equal(original.Id, Assert.Single(originals.Items).Id);
    }

    public async Task DisposeAsync()
    {
        await using var context = new ArtaFinDbContext(_options);
        await context.Database.EnsureDeletedAsync();
    }
}
