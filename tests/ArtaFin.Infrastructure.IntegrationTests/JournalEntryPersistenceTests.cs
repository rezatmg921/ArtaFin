using System.Data.Common;
using ArtaFin.Domain.Finance;
using ArtaFin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArtaFin.Infrastructure.IntegrationTests;

public sealed class JournalEntryPersistenceTests : IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly DbContextOptions<ArtaFinDbContext> _options;

    public JournalEntryPersistenceTests()
    {
        var configuredConnectionString =
            Environment.GetEnvironmentVariable("ARTAFIN_SQLSERVER")
            ?? throw new InvalidOperationException(
                "Set ARTAFIN_SQLSERVER to a SQL Server connection string for integration tests.");

        var connectionStringBuilder = new DbConnectionStringBuilder
        {
            ConnectionString = configuredConnectionString
        };
        connectionStringBuilder["Initial Catalog"] = $"ArtaFin_Integration_{Guid.NewGuid():N}";
        _connectionString = connectionStringBuilder.ConnectionString;

        _options = new DbContextOptionsBuilder<ArtaFinDbContext>()
            .UseSqlServer(_connectionString)
            .Options;
    }

    public async Task InitializeAsync()
    {
        await using var context = new ArtaFinDbContext(_options);
        await context.Database.EnsureCreatedAsync();
    }

    [Fact]
    public async Task SaveAndReload_PreservesJournalEntryLinesAndMoney()
    {
        var entryId = Guid.NewGuid();
        var financialBookId = Guid.NewGuid();
        var cashAccountId = Guid.NewGuid();
        var revenueAccountId = Guid.NewGuid();
        var postedAt = new DateTimeOffset(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);

        var entry = JournalEntry.Post(
            entryId,
            financialBookId,
            postedAt,
            "Initial cash receipt",
            [
                new JournalLine(cashAccountId, PostingSide.Debit, new Money(1250.7500m, "USD")),
                new JournalLine(revenueAccountId, PostingSide.Credit, new Money(1250.7500m, "USD"))
            ]);

        await using (var writeContext = new ArtaFinDbContext(_options))
        {
            writeContext.JournalEntries.Add(entry);
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = new ArtaFinDbContext(_options);
        var reloaded = await readContext.JournalEntries
            .AsNoTracking()
            .Include(x => x.Lines)
            .SingleAsync(x => x.Id == entryId);

        Assert.Equal(entryId, reloaded.Id);
        Assert.Equal(financialBookId, reloaded.FinancialBookId);
        Assert.Equal(postedAt, reloaded.PostedAt);
        Assert.Equal("Initial cash receipt", reloaded.Description);
        Assert.Equal(new[] { 1, 2 }, reloaded.Lines.Select(x => x.LineNumber).ToArray());

        Assert.Equal(cashAccountId, reloaded.Lines[0].AccountId);
        Assert.Equal(PostingSide.Debit, reloaded.Lines[0].Side);
        Assert.Equal(1250.7500m, reloaded.Lines[0].Amount.Amount);
        Assert.Equal("USD", reloaded.Lines[0].Amount.Currency);

        Assert.Equal(revenueAccountId, reloaded.Lines[1].AccountId);
        Assert.Equal(PostingSide.Credit, reloaded.Lines[1].Side);
        Assert.Equal(1250.7500m, reloaded.Lines[1].Amount.Amount);
        Assert.Equal("USD", reloaded.Lines[1].Amount.Currency);
    }

    public async Task DisposeAsync()
    {
        await using var context = new ArtaFinDbContext(_options);
        await context.Database.EnsureDeletedAsync();
    }
}
