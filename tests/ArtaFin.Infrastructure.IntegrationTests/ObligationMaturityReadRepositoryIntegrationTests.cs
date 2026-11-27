using ArtaFin.Application;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Reports.GetBalanceSheet;
using ArtaFin.Domain.Contacts;
using ArtaFin.Domain.Finance;
using ArtaFin.Domain.Obligations;
using ArtaFin.Infrastructure;
using ArtaFin.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ArtaFin.Infrastructure.IntegrationTests;

public sealed class ObligationMaturityReadRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly string _connectionString = IntegrationTestDatabase.GetConnectionString();
    private DbContextOptions<ArtaFinDbContext> Options => new DbContextOptionsBuilder<ArtaFinDbContext>()
        .UseSqlServer(_connectionString)
        .Options;

    public async Task InitializeAsync()
    {
        await using var context = new ArtaFinDbContext(Options);
        await context.Database.MigrateAsync();
    }

    [Fact]
    public async Task Handle_UsesInstallmentsAndHistoricalPaymentAndReversalDates()
    {
        var asOf = new DateTimeOffset(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);
        var ownerId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var book = FinancialBook.Create(bookId, ownerId, "Obligation maturity integration", "USD", asOf.AddDays(-30));
        var asset = Account.Create(Guid.NewGuid(), bookId, "1000", "Cash", AccountType.Asset, asOf.AddDays(-30));
        var liability = Account.Create(Guid.NewGuid(), bookId, "2000", "Payables", AccountType.Liability, asOf.AddDays(-30));
        var contact = Contact.Create(Guid.NewGuid(), bookId, ContactType.Organization, "Supplier", null, null, null, asOf.AddDays(-30));
        var obligation = FinancialObligation.Create(
            Guid.NewGuid(), bookId, contact.Id, liability.Id,
            FinancialObligationDirection.Payable, FinancialObligationKind.GeneralDebt,
            100m, "USD", new DateOnly(2026, 10, 1), new DateOnly(2028, 2, 1),
            "Installment payable", null, asOf.AddDays(-20),
            [
                new FinancialObligationInstallmentTerm(30m, new DateOnly(2027, 2, 1)),
                new FinancialObligationInstallmentTerm(70m, new DateOnly(2028, 2, 1))
            ]);

        var originalEntry = JournalEntry.Post(
            Guid.NewGuid(), bookId, asOf.AddDays(-10), "Recognize payable",
            [
                new JournalLine(asset.Id, PostingSide.Debit, new Money(100m, "USD")),
                new JournalLine(liability.Id, PostingSide.Credit, new Money(100m, "USD"))
            ]);
        var paymentEntry = JournalEntry.Post(
            Guid.NewGuid(), bookId, asOf.AddDays(-5), "Pay first installment",
            [
                new JournalLine(liability.Id, PostingSide.Debit, new Money(20m, "USD")),
                new JournalLine(asset.Id, PostingSide.Credit, new Money(20m, "USD"))
            ]);
        var reversalEntry = JournalEntry.Post(
            Guid.NewGuid(), bookId, asOf.AddDays(2), "Reverse payment after report date",
            [
                new JournalLine(asset.Id, PostingSide.Debit, new Money(20m, "USD")),
                new JournalLine(liability.Id, PostingSide.Credit, new Money(20m, "USD"))
            ]);
        var futurePaymentEntry = JournalEntry.Post(
            Guid.NewGuid(), bookId, asOf.AddDays(3), "Backdated payment recorded after report date",
            [
                new JournalLine(liability.Id, PostingSide.Debit, new Money(10m, "USD")),
                new JournalLine(asset.Id, PostingSide.Credit, new Money(10m, "USD"))
            ]);

        var firstInstallmentId = obligation.Installments[0].Id;
        var secondInstallmentId = obligation.Installments[1].Id;
        var reversedPayment = FinancialObligationPayment.Create(
            Guid.NewGuid(), bookId, obligation.Id, asset.Id, paymentEntry.Id,
            20m, "USD", DateOnly.FromDateTime(asOf.AddDays(-5).DateTime),
            "First installment payment", null, ownerId, asOf.AddDays(-5),
            "maturity-reversed-payment", new string('A', 64),
            [new FinancialObligationPaymentAllocationTerm(firstInstallmentId, 20m)]);
        reversedPayment.MarkReversed(
            reversalEntry.Id, ownerId, asOf.AddDays(2), "Reversed after reporting date",
            "maturity-reversal", new string('B', 64));

        var futurePayment = FinancialObligationPayment.Create(
            Guid.NewGuid(), bookId, obligation.Id, asset.Id, futurePaymentEntry.Id,
            10m, "USD", DateOnly.FromDateTime(asOf.AddDays(3).DateTime),
            "Future installment payment", null, ownerId, asOf.AddDays(3),
            "maturity-future-payment", new string('C', 64),
            [new FinancialObligationPaymentAllocationTerm(secondInstallmentId, 10m)]);

        await using (var write = new ArtaFinDbContext(Options))
        {
            write.FinancialBooks.Add(book);
            write.Accounts.AddRange(asset, liability);
            write.Contacts.Add(contact);
            write.FinancialObligations.Add(obligation);
            write.JournalEntries.AddRange(originalEntry, paymentEntry, reversalEntry, futurePaymentEntry);
            write.FinancialObligationPayments.AddRange(reversedPayment, futurePayment);
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
        var result = await sender.Send(new GetBalanceSheetQuery(bookId, asOf), CancellationToken.None);

        var maturity = Assert.Single(result.ObligationMaturities);
        Assert.Equal(10m, maturity.CurrentAmount);
        Assert.Equal(70m, maturity.NonCurrentAmount);
        Assert.Equal(80m, maturity.TotalOutstanding);
        Assert.Equal(80m, maturity.LedgerBalance);
        Assert.Equal(0m, maturity.ReconciliationDifference);
        Assert.True(maturity.IsReconciled);
        Assert.Equal(80m, result.TotalLiabilities);
    }

    public async Task DisposeAsync()
    {
        await using var context = new ArtaFinDbContext(Options);
        await context.Database.EnsureDeletedAsync();
    }

    private sealed class TestCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }
}
