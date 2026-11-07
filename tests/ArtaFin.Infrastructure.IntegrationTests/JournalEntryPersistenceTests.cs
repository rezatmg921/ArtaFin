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
        var configuredConnectionString = Environment.GetEnvironmentVariable("ARTAFIN_SQLSERVER")
            ?? throw new InvalidOperationException("Set ARTAFIN_SQLSERVER to a SQL Server connection string.");
        var builder = new DbConnectionStringBuilder { ConnectionString = configuredConnectionString };
        builder["Initial Catalog"] = $"ArtaFin_Integration_{Guid.NewGuid():N}";
        _connectionString = builder.ConnectionString;
        _options = new DbContextOptionsBuilder<ArtaFinDbContext>().UseSqlServer(_connectionString).Options;
    }

    public async Task InitializeAsync()
    {
        await using var context = new ArtaFinDbContext(_options);
        await context.Database.MigrateAsync();
    }

    [Fact]
    public async Task Database_MigrationsCreateTheExpectedSchema()
    {
        await using var context = new ArtaFinDbContext(_options);

        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();

        Assert.Contains("20261107170000_InitialCreate", appliedMigrations);
        Assert.True(await context.Database.CanConnectAsync());
    }

    [Fact]
    public async Task SaveAndReload_PreservesJournalEntryLinesAndMoney()
    {
        var entryId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var cashId = Guid.NewGuid();
        var incomeId = Guid.NewGuid();
        var postedAt = new DateTimeOffset(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);

        var book = FinancialBook.Create(bookId, ownerId, "Personal finances", "USD", postedAt);
        var cash = Account.Create(cashId, bookId, "1000", "Cash", AccountType.Asset, postedAt);
        var income = Account.Create(incomeId, bookId, "4000", "Income", AccountType.Income, postedAt);
        var entry = JournalEntry.Post(entryId, bookId, postedAt, "Initial cash receipt",
        [
            new JournalLine(cashId, PostingSide.Debit, new Money(1250.7500m, "USD")),
            new JournalLine(incomeId, PostingSide.Credit, new Money(1250.7500m, "USD"))
        ]);

        await using (var write = new ArtaFinDbContext(_options))
        {
            write.FinancialBooks.Add(book);
            write.Accounts.AddRange(cash, income);
            write.JournalEntries.Add(entry);
            await write.SaveChangesAsync();
        }

        await using var read = new ArtaFinDbContext(_options);
        var actualBook = await read.FinancialBooks.AsNoTracking()
            .SingleAsync(x => x.Id == bookId);
        var actual = await read.JournalEntries.AsNoTracking().Include(x => x.Lines)
            .SingleAsync(x => x.Id == entryId);

        Assert.Equal("USD", actualBook.BaseCurrency.Code);
        Assert.Equal(2, actualBook.BaseCurrency.MinorUnit);
        Assert.Equal(entryId, actual.Id);
        Assert.Equal(bookId, actual.FinancialBookId);
        Assert.Equal(postedAt, actual.PostedAt);
        Assert.Equal("Initial cash receipt", actual.Description);
        Assert.Equal(new[] { 1, 2 }, actual.Lines.Select(x => x.LineNumber).ToArray());
        Assert.Equal(bookId, actual.Lines[0].FinancialBookId);
        Assert.Equal(cashId, actual.Lines[0].AccountId);
        Assert.Equal(PostingSide.Debit, actual.Lines[0].Side);
        Assert.Equal(1250.7500m, actual.Lines[0].Amount.Amount);
        Assert.Equal("USD", actual.Lines[0].Amount.Currency);
        Assert.Equal(incomeId, actual.Lines[1].AccountId);
        Assert.Equal(PostingSide.Credit, actual.Lines[1].Side);
    }

    [Fact]
    public async Task Accounts_RejectDuplicateCodesWithinTheSameFinancialBook()
    {
        var now = DateTimeOffset.UtcNow;
        var bookId = Guid.NewGuid();
        await using var context = new ArtaFinDbContext(_options);
        context.FinancialBooks.Add(FinancialBook.Create(bookId, Guid.NewGuid(), "Personal", "USD", now));
        context.Accounts.AddRange(
            Account.Create(Guid.NewGuid(), bookId, "1000", "Cash", AccountType.Asset, now),
            Account.Create(Guid.NewGuid(), bookId, "1000", "Bank cash", AccountType.Asset, now));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task JournalLine_RejectsAccountFromAnotherFinancialBook()
    {
        var now = DateTimeOffset.UtcNow;
        var entryBookId = Guid.NewGuid();
        var otherBookId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var validAccountId = Guid.NewGuid();

        await using var context = new ArtaFinDbContext(_options);
        context.FinancialBooks.AddRange(
            FinancialBook.Create(entryBookId, Guid.NewGuid(), "Book A", "USD", now),
            FinancialBook.Create(otherBookId, Guid.NewGuid(), "Book B", "USD", now));
        context.Accounts.AddRange(
            Account.Create(accountId, otherBookId, "1000", "Cash", AccountType.Asset, now),
            Account.Create(validAccountId, entryBookId, "2000", "Payable", AccountType.Liability, now));

        var entry = JournalEntry.Post(Guid.NewGuid(), entryBookId, now, "Cross-book reference",
        [
            new JournalLine(accountId, PostingSide.Debit, new Money(10m, "USD")),
            new JournalLine(validAccountId, PostingSide.Credit, new Money(10m, "USD"))
        ]);
        context.JournalEntries.Add(entry);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    public async Task DisposeAsync()
    {
        await using var context = new ArtaFinDbContext(_options);
        await context.Database.EnsureDeletedAsync();
    }
}
