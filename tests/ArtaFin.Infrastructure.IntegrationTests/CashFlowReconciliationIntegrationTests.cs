using ArtaFin.Application;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Reports.GetCashFlowReconciliation;
using ArtaFin.Domain.Finance;
using ArtaFin.Infrastructure;
using ArtaFin.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ArtaFin.Infrastructure.IntegrationTests;

public sealed class CashFlowReconciliationIntegrationTests : IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly DbContextOptions<ArtaFinDbContext> _options;

    public CashFlowReconciliationIntegrationTests()
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
    public async Task Handle_ReconcilesSqlServerCashTurnoverWithCashFlowStatement()
    {
        var from = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddMonths(1);
        var ownerId = Guid.NewGuid();
        var bookId = Guid.NewGuid();

        var book = FinancialBook.Create(
            bookId, ownerId, "Cash flow reconciliation integration", "USD", from.AddDays(-10));
        var cash = Account.Create(
            Guid.NewGuid(), bookId, "1000", "Cash", AccountType.Asset, from.AddDays(-10));
        cash.ConfigureCashFlowClassification(CashFlowAccountType.Cash);
        var bank = Account.Create(
            Guid.NewGuid(), bookId, "1010", "Bank", AccountType.Asset, from.AddDays(-10));
        bank.ConfigureCashFlowClassification(CashFlowAccountType.Cash);
        var capital = Account.Create(
            Guid.NewGuid(), bookId, "3000", "Capital", AccountType.Equity, from.AddDays(-10));
        var income = Account.Create(
            Guid.NewGuid(), bookId, "4000", "Service revenue", AccountType.Income, from.AddDays(-10));
        var expense = Account.Create(
            Guid.NewGuid(), bookId, "6000", "Supplies", AccountType.Expense, from.AddDays(-10));

        var opening = JournalEntry.Post(
            Guid.NewGuid(), bookId, from.AddDays(-2), "Opening cash balance",
            [
                new JournalLine(cash.Id, PostingSide.Debit, new Money(100m, "USD")),
                new JournalLine(capital.Id, PostingSide.Credit, new Money(100m, "USD"))
            ]);

        var receipt = JournalEntry.Post(
            Guid.NewGuid(), bookId, from.AddDays(2), "Customer receipt",
            [
                new JournalLine(cash.Id, PostingSide.Debit, new Money(50m, "USD")),
                new JournalLine(income.Id, PostingSide.Credit, new Money(50m, "USD"))
            ]);

        var payment = JournalEntry.Post(
            Guid.NewGuid(), bookId, from.AddDays(3), "Supplies payment",
            [
                new JournalLine(expense.Id, PostingSide.Debit, new Money(20m, "USD")),
                new JournalLine(cash.Id, PostingSide.Credit, new Money(20m, "USD"))
            ]);

        var internalTransfer = JournalEntry.Post(
            Guid.NewGuid(), bookId, from.AddDays(4), "Cash to bank",
            [
                new JournalLine(cash.Id, PostingSide.Debit, new Money(10m, "USD")),
                new JournalLine(bank.Id, PostingSide.Credit, new Money(10m, "USD"))
            ]);

        var receiptAllocation = CashFlowAllocationSet.Create(
            Guid.NewGuid(), bookId, receipt.Id, 1, 50m, "USD", ownerId, from.AddDays(2));
        receiptAllocation.AddAllocation(
            Guid.NewGuid(), 50m, CashFlowActivity.Operating, "CUSTOMER_RECEIPTS", 2, "Customer receipt");
        receiptAllocation.Finalize(ownerId, from.AddDays(2).AddMinutes(1));

        var paymentAllocation = CashFlowAllocationSet.Create(
            Guid.NewGuid(), bookId, payment.Id, 2, 20m, "USD", ownerId, from.AddDays(3));
        paymentAllocation.AddAllocation(
            Guid.NewGuid(), 20m, CashFlowActivity.Operating, "OPERATING_SUPPLIER_PAYMENT", 1, "Supplies");
        paymentAllocation.Finalize(ownerId, from.AddDays(3).AddMinutes(1));

        await using (var write = new ArtaFinDbContext(_options))
        {
            write.FinancialBooks.Add(book);
            write.Accounts.AddRange(cash, bank, capital, income, expense);
            write.JournalEntries.AddRange(opening, receipt, payment, internalTransfer);
            await write.SaveChangesAsync();

            // SQL Server enforces the source-line foreign key; persist journal entries first.
            write.CashFlowAllocationSets.AddRange(receiptAllocation, paymentAllocation);
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
            new GetCashFlowReconciliationQuery(bookId, from, to),
            CancellationToken.None);

        Assert.True(result.IsReconciled);
        Assert.Equal(from, result.From);
        Assert.Equal(to, result.To);
        Assert.Equal(to, result.TrialBalanceAsOf);
        Assert.Equal("USD", result.Currency);

        Assert.Equal(60m, result.CashAccountDebits);
        Assert.Equal(30m, result.CashAccountCredits);
        Assert.Equal(10m, result.InternalTransfers);
        Assert.Equal(50m, result.ExternalCashInflowsFromTrialBalance);
        Assert.Equal(20m, result.ExternalCashOutflowsFromTrialBalance);
        Assert.Equal(50m, result.CashFlowReceipts);
        Assert.Equal(20m, result.CashFlowPayments);
        Assert.Equal(0m, result.ReceiptsDifference);
        Assert.Equal(0m, result.PaymentsDifference);

        Assert.Equal(2, result.Accounts.Count);
        Assert.Contains(result.Accounts, account =>
            account.AccountId == cash.Id && account.Debits == 60m && account.Credits == 20m);
        Assert.Contains(result.Accounts, account =>
            account.AccountId == bank.Id && account.Debits == 0m && account.Credits == 10m);
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
