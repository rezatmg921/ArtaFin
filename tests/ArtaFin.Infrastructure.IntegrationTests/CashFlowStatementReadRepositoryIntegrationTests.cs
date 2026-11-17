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

public sealed class CashFlowStatementReadRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly DbContextOptions<ArtaFinDbContext> _options;

    public CashFlowStatementReadRepositoryIntegrationTests()
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
        builder["Initial Catalog"] = $"ArtaFin_CashFlowStatement_{Guid.NewGuid():N}";
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
    public async Task GetBeforeAsync_ReturnsCashEntriesCounterpartsAndCurrentAllocationStates()
    {
        var now = new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
        var bookId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var book = FinancialBook.Create(bookId, ownerId, "Cash flow statement integration", "USD", now.AddDays(-10));

        var cash = Account.Create(Guid.NewGuid(), bookId, "1000", "Cash", AccountType.Asset, now.AddDays(-10));
        cash.ConfigureCashFlowClassification(CashFlowAccountType.Cash);
        var bank = Account.Create(Guid.NewGuid(), bookId, "1010", "Bank", AccountType.Asset, now.AddDays(-10));
        bank.ConfigureCashFlowClassification(CashFlowAccountType.Cash);
        var capital = Account.Create(Guid.NewGuid(), bookId, "3000", "Capital", AccountType.Equity, now.AddDays(-10));
        var expense = Account.Create(Guid.NewGuid(), bookId, "6000", "Supplies", AccountType.Expense, now.AddDays(-10));
        var income = Account.Create(Guid.NewGuid(), bookId, "4000", "Service revenue", AccountType.Income, now.AddDays(-10));

        var contribution = JournalEntry.Post(Guid.NewGuid(), bookId, now.AddDays(-3), "Owner contribution",
        [
            new JournalLine(cash.Id, PostingSide.Debit, new Money(100m, "USD")),
            new JournalLine(capital.Id, PostingSide.Credit, new Money(100m, "USD"))
        ]);
        var payment = JournalEntry.Post(Guid.NewGuid(), bookId, now.AddDays(-2), "Supplies payment",
        [
            new JournalLine(expense.Id, PostingSide.Debit, new Money(30m, "USD")),
            new JournalLine(cash.Id, PostingSide.Credit, new Money(30m, "USD"))
        ]);
        var internalTransfer = JournalEntry.Post(Guid.NewGuid(), bookId, now.AddDays(-1), "Cash to bank",
        [
            new JournalLine(bank.Id, PostingSide.Debit, new Money(20m, "USD")),
            new JournalLine(cash.Id, PostingSide.Credit, new Money(20m, "USD"))
        ]);
        var unclassified = JournalEntry.Post(Guid.NewGuid(), bookId, now.AddHours(-12), "Unclassified receipt",
        [
            new JournalLine(cash.Id, PostingSide.Debit, new Money(15m, "USD")),
            new JournalLine(income.Id, PostingSide.Credit, new Money(15m, "USD"))
        ]);
        var atExclusiveEnd = JournalEntry.Post(Guid.NewGuid(), bookId, now, "Late receipt",
        [
            new JournalLine(cash.Id, PostingSide.Debit, new Money(10m, "USD")),
            new JournalLine(income.Id, PostingSide.Credit, new Money(10m, "USD"))
        ]);

        var contributionSet = CashFlowAllocationSet.Create(
            Guid.NewGuid(), bookId, contribution.Id, 1, 100m, "USD", ownerId, now.AddDays(-3));
        contributionSet.AddAllocation(
            Guid.NewGuid(), 100m, CashFlowActivity.Financing, "OWNER_CONTRIBUTION", 2, "Owner funds");
        contributionSet.Finalize(ownerId, now.AddDays(-3).AddMinutes(1));

        var paymentSet = CashFlowAllocationSet.Create(
            Guid.NewGuid(), bookId, payment.Id, 2, 30m, "USD", ownerId, now.AddDays(-2));
        paymentSet.AddAllocation(
            Guid.NewGuid(), 30m, CashFlowActivity.Operating, "OPERATING_SUPPLIER_PAYMENT", 1, "Supplies");
        paymentSet.Finalize(ownerId, now.AddDays(-2).AddMinutes(1));

        var draftSet = CashFlowAllocationSet.Create(
            Guid.NewGuid(), bookId, unclassified.Id, 1, 15m, "USD", ownerId, now.AddHours(-12));

        await using (var write = new ArtaFinDbContext(_options))
        {
            write.FinancialBooks.Add(book);
            write.Accounts.AddRange(cash, bank, capital, expense, income);
            write.JournalEntries.AddRange(contribution, payment, internalTransfer, unclassified, atExclusiveEnd);
            write.CashFlowAllocationSets.AddRange(contributionSet, paymentSet, draftSet);
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
        var repository = scope.ServiceProvider.GetRequiredService<ICashFlowStatementReadRepository>();

        var result = await repository.GetBeforeAsync(
            bookId, now, CancellationToken.None);

        Assert.Equal(8, result.Lines.Count);
        Assert.Equal(2, result.FinalizedAllocations.Count);
        Assert.Single(result.Drafts);
        Assert.Contains(result.Lines, line =>
            line.JournalEntryId == contribution.Id &&
            line.LineNumber == 2 &&
            line.AccountId == capital.Id);
        Assert.Contains(result.Lines, line =>
            line.JournalEntryId == payment.Id &&
            line.LineNumber == 1 &&
            line.AccountId == expense.Id);
        Assert.Contains(result.Lines, line => line.JournalEntryId == unclassified.Id);
        Assert.DoesNotContain(result.Lines, line => line.JournalEntryId == atExclusiveEnd.Id);
        Assert.DoesNotContain(result.FinalizedAllocations, allocation =>
            allocation.JournalEntryId == internalTransfer.Id);
    }

    public async Task DisposeAsync()
    {
        await using var context = new ArtaFinDbContext(_options);
        await context.Database.EnsureDeletedAsync();
    }
}
