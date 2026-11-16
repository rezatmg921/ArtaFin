using System.Data.Common;
using ArtaFin.Application.Features.Accounts.CreateAccount;
using ArtaFin.Domain.Finance;
using ArtaFin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ArtaFin.Infrastructure.IntegrationTests;

public sealed class JournalEntryPersistenceTests : IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly DbContextOptions<ArtaFinDbContext> _options;

    public JournalEntryPersistenceTests()
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
        builder["Initial Catalog"] = $"ArtaFin_JournalEntry_{Guid.NewGuid():N}";
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

        Assert.Contains("20261108051753_InitialCreate", appliedMigrations);
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
    public async Task SaveAndReload_ExpenseJournalEntry_PreservesExpenseAndPaymentLines()
    {
        var now = new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);
        var bookId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var expenseAccountId = Guid.NewGuid();
        var paymentAccountId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        var book = FinancialBook.Create(bookId, ownerId, "Expense persistence test", "EUR", now);
        var expenseAccount = Account.Create(
            expenseAccountId, bookId, "6000", "Office supplies", AccountType.Expense, now);
        var paymentAccount = Account.Create(
            paymentAccountId, bookId, "1000", "Bank", AccountType.Asset, now);
        var expenseEntry = JournalEntry.Post(
            entryId,
            bookId,
            now,
            "Office supplies",
            [
                new JournalLine(expenseAccountId, PostingSide.Debit, new Money(125m, "EUR")),
                new JournalLine(paymentAccountId, PostingSide.Credit, new Money(125m, "EUR"))
            ]);

        await using (var write = new ArtaFinDbContext(_options))
        {
            write.FinancialBooks.Add(book);
            write.Accounts.AddRange(expenseAccount, paymentAccount);
            write.JournalEntries.Add(expenseEntry);
            await write.SaveChangesAsync();
        }

        await using var read = new ArtaFinDbContext(_options);
        var persisted = await read.JournalEntries
            .AsNoTracking()
            .Include(entry => entry.Lines)
            .SingleAsync(entry => entry.Id == entryId);

        Assert.Equal(bookId, persisted.FinancialBookId);
        Assert.Equal("Office supplies", persisted.Description);
        Assert.Equal(2, persisted.Lines.Count);

        var debit = Assert.Single(persisted.Lines, line => line.Side == PostingSide.Debit);
        Assert.Equal(expenseAccountId, debit.AccountId);
        Assert.Equal(125m, debit.Amount.Amount);
        Assert.Equal("EUR", debit.Amount.Currency);

        var credit = Assert.Single(persisted.Lines, line => line.Side == PostingSide.Credit);
        Assert.Equal(paymentAccountId, credit.AccountId);
        Assert.Equal(125m, credit.Amount.Amount);
        Assert.Equal("EUR", credit.Amount.Currency);
    }

    [Fact]
    public async Task SaveAndReload_IncomeJournalEntry_PreservesDepositAndIncomeLines()
    {
        var now = new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);
        var bookId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var depositAccountId = Guid.NewGuid();
        var incomeAccountId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        var book = FinancialBook.Create(bookId, ownerId, "Income persistence test", "IRR", now);
        var depositAccount = Account.Create(
            depositAccountId, bookId, "1000", "Bank", AccountType.Asset, now);
        var incomeAccount = Account.Create(
            incomeAccountId, bookId, "4000", "Sales income", AccountType.Income, now);
        var incomeEntry = JournalEntry.Post(
            entryId,
            bookId,
            now,
            "Product sales",
            [
                new JournalLine(depositAccountId, PostingSide.Debit, new Money(12500m, "IRR")),
                new JournalLine(incomeAccountId, PostingSide.Credit, new Money(12500m, "IRR"))
            ]);

        await using (var write = new ArtaFinDbContext(_options))
        {
            write.FinancialBooks.Add(book);
            write.Accounts.AddRange(depositAccount, incomeAccount);
            write.JournalEntries.Add(incomeEntry);
            await write.SaveChangesAsync();
        }

        await using var read = new ArtaFinDbContext(_options);
        var persisted = await read.JournalEntries
            .AsNoTracking()
            .Include(entry => entry.Lines)
            .SingleAsync(entry => entry.Id == entryId);

        Assert.Equal(bookId, persisted.FinancialBookId);
        Assert.Equal("Product sales", persisted.Description);
        Assert.Equal(2, persisted.Lines.Count);

        var debit = Assert.Single(persisted.Lines, line => line.Side == PostingSide.Debit);
        Assert.Equal(depositAccountId, debit.AccountId);
        Assert.Equal(12500m, debit.Amount.Amount);
        Assert.Equal("IRR", debit.Amount.Currency);

        var credit = Assert.Single(persisted.Lines, line => line.Side == PostingSide.Credit);
        Assert.Equal(incomeAccountId, credit.AccountId);
        Assert.Equal(12500m, credit.Amount.Amount);
        Assert.Equal("IRR", credit.Amount.Currency);
    }

    [Fact]
    public async Task SaveAndReload_TransferJournalEntry_PreservesSourceAndDestinationLines()
    {
        var now = new DateTimeOffset(2026, 11, 16, 11, 0, 0, TimeSpan.Zero);
        var bookId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var sourceAccountId = Guid.NewGuid();
        var destinationAccountId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        var book = FinancialBook.Create(bookId, ownerId, "Transfer persistence test", "USD", now);
        var source = Account.Create(sourceAccountId, bookId, "1000", "Main bank", AccountType.Asset, now);
        var destination = Account.Create(destinationAccountId, bookId, "1010", "Savings", AccountType.Asset, now);
        var transfer = JournalEntry.Post(
            entryId, bookId, now, "Move to savings",
            [
                new JournalLine(destinationAccountId, PostingSide.Debit, new Money(500m, "USD")),
                new JournalLine(sourceAccountId, PostingSide.Credit, new Money(500m, "USD"))
            ]);

        await using (var write = new ArtaFinDbContext(_options))
        {
            write.FinancialBooks.Add(book);
            write.Accounts.AddRange(source, destination);
            write.JournalEntries.Add(transfer);
            await write.SaveChangesAsync();
        }

        await using var read = new ArtaFinDbContext(_options);
        var persisted = await read.JournalEntries.AsNoTracking()
            .Include(entry => entry.Lines)
            .SingleAsync(entry => entry.Id == entryId);

        Assert.Equal(bookId, persisted.FinancialBookId);
        Assert.Equal("Move to savings", persisted.Description);
        Assert.Equal(2, persisted.Lines.Count);

        var debit = Assert.Single(persisted.Lines, line => line.Side == PostingSide.Debit);
        Assert.Equal(destinationAccountId, debit.AccountId);
        Assert.Equal(500m, debit.Amount.Amount);
        Assert.Equal("USD", debit.Amount.Currency);

        var credit = Assert.Single(persisted.Lines, line => line.Side == PostingSide.Credit);
        Assert.Equal(sourceAccountId, credit.AccountId);
        Assert.Equal(500m, credit.Amount.Amount);
        Assert.Equal("USD", credit.Amount.Currency);
    }

    [Fact]
    public async Task PostedJournalEntry_RejectsDescriptionChanges()
    {
        var now = DateTimeOffset.UtcNow;
        var bookId = Guid.NewGuid();
        var cashId = Guid.NewGuid();
        var incomeId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        await using (var write = new ArtaFinDbContext(_options))
        {
            write.FinancialBooks.Add(FinancialBook.Create(
                bookId, Guid.NewGuid(), "Immutability test", "USD", now));
            write.Accounts.AddRange(
                Account.Create(cashId, bookId, "1000", "Cash", AccountType.Asset, now),
                Account.Create(incomeId, bookId, "4000", "Income", AccountType.Income, now));
            write.JournalEntries.Add(JournalEntry.Post(
                entryId, bookId, now, "Original description",
                [
                    new JournalLine(cashId, PostingSide.Debit, new Money(25m, "USD")),
                    new JournalLine(incomeId, PostingSide.Credit, new Money(25m, "USD"))
                ]));
            await write.SaveChangesAsync();
        }

        await using (var update = new ArtaFinDbContext(_options))
        {
            var entry = await update.JournalEntries
                .SingleAsync(x => x.Id == entryId);

            update.Entry(entry)
                .Property(x => x.Description)
                .CurrentValue = "Changed description";

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => update.SaveChangesAsync());
        }

        await using var verify = new ArtaFinDbContext(_options);
        var persisted = await verify.JournalEntries
            .AsNoTracking()
            .SingleAsync(x => x.Id == entryId);

        Assert.Equal("Original description", persisted.Description);
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
        await Assert.ThrowsAsync<AccountCodeAlreadyExistsException>(() => context.SaveChangesAsync());
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

    [Fact]
    public async Task JournalEntry_RejectsMoreThanOneReversalForOriginal()
    {
        var now = DateTimeOffset.UtcNow;
        var bookId = Guid.NewGuid();
        var cashId = Guid.NewGuid();
        var incomeId = Guid.NewGuid();
        var originalId = Guid.NewGuid();

        await using (var setup = new ArtaFinDbContext(_options))
        {
            setup.FinancialBooks.Add(FinancialBook.Create(
                bookId, Guid.NewGuid(), "Reversal uniqueness test", "USD", now));
            setup.Accounts.AddRange(
                Account.Create(cashId, bookId, "1000", "Cash", AccountType.Asset, now),
                Account.Create(incomeId, bookId, "4000", "Income", AccountType.Income, now));
            setup.JournalEntries.Add(JournalEntry.Post(
                originalId, bookId, now, "Original entry",
                [
                    new JournalLine(cashId, PostingSide.Debit, new Money(30m, "USD")),
                    new JournalLine(incomeId, PostingSide.Credit, new Money(30m, "USD"))
                ]));
            await setup.SaveChangesAsync();
        }

        await using (var firstReversalContext = new ArtaFinDbContext(_options))
        {
            var original = await firstReversalContext.JournalEntries
                .Include(entry => entry.Lines)
                .SingleAsync(entry => entry.Id == originalId);
            firstReversalContext.JournalEntries.Add(JournalEntry.Reverse(
                Guid.NewGuid(), original, now.AddMinutes(1), "First reversal"));
            await firstReversalContext.SaveChangesAsync();
        }

        await using (var secondReversalContext = new ArtaFinDbContext(_options))
        {
            var original = await secondReversalContext.JournalEntries
                .Include(entry => entry.Lines)
                .SingleAsync(entry => entry.Id == originalId);
            secondReversalContext.JournalEntries.Add(JournalEntry.Reverse(
                Guid.NewGuid(), original, now.AddMinutes(2), "Second reversal"));

            await Assert.ThrowsAsync<DbUpdateException>(
                () => secondReversalContext.SaveChangesAsync());
        }
    }

    public async Task DisposeAsync()
    {
        await using var context = new ArtaFinDbContext(_options);
        await context.Database.EnsureDeletedAsync();
    }
}
