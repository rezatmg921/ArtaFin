using System.Data.Common;
using ArtaFin.Application;
using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.CashFlowNonCashDisclosures;
using ArtaFin.Domain.Finance;
using ArtaFin.Infrastructure;
using ArtaFin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ArtaFin.Infrastructure.IntegrationTests;

public sealed class CashFlowNonCashDisclosureServiceIntegrationTests : IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly DbContextOptions<ArtaFinDbContext> _options;

    public CashFlowNonCashDisclosureServiceIntegrationTests()
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
    public async Task CreateDisclosure_ValidatesSource_PersistsAuditRecord_AndFeedsReportReadModel()
    {
        var now = new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);
        var ownerId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var equipmentId = Guid.NewGuid();
        var leaseLiabilityId = Guid.NewGuid();
        var cashId = Guid.NewGuid();
        var journalEntryId = Guid.NewGuid();

        var book = FinancialBook.Create(bookId, ownerId, "Non-cash disclosure integration", "USD", now.AddDays(-5));
        var equipment = Account.Create(equipmentId, bookId, "1500", "Equipment", AccountType.Asset, now.AddDays(-5));
        var leaseLiability = Account.Create(leaseLiabilityId, bookId, "2500", "Lease liability", AccountType.Liability, now.AddDays(-5));
        var cash = Account.Create(cashId, bookId, "1000", "Cash", AccountType.Asset, now.AddDays(-5));
        cash.ConfigureCashFlowClassification(CashFlowAccountType.Cash);

        var leaseEntry = JournalEntry.Post(
            journalEntryId, bookId, now, "Equipment recognized through finance lease",
            [
                new JournalLine(equipmentId, PostingSide.Debit, new Money(125_000m, "USD")),
                new JournalLine(leaseLiabilityId, PostingSide.Credit, new Money(125_000m, "USD"))
            ]);
        var cashEntry = JournalEntry.Post(
            Guid.NewGuid(), bookId, now.AddHours(1), "Cash receipt",
            [
                new JournalLine(cashId, PostingSide.Debit, new Money(25m, "USD")),
                new JournalLine(leaseLiabilityId, PostingSide.Credit, new Money(25m, "USD"))
            ]);

        await using (var setup = new ArtaFinDbContext(_options))
        {
            setup.FinancialBooks.Add(book);
            setup.Accounts.AddRange(equipment, leaseLiability, cash);
            setup.JournalEntries.AddRange(leaseEntry, cashEntry);
            await setup.SaveChangesAsync();
        }

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ArtaFin"] = _connectionString
            })
            .Build();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddScoped<ICurrentUser>(_ => new TestCurrentUser(ownerId));

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<CashFlowNonCashDisclosureService>();

        var created = await service.CreateAsync(
            bookId,
            journalEntryId,
            2,
            new CreateCashFlowNonCashDisclosureInput(
                125_000m, "usd", CashFlowActivity.Financing,
                "ASSET_ACQUIRED_THROUGH_LEASE", "No cash paid at commencement"),
            CancellationToken.None);

        Assert.Equal(journalEntryId, created.JournalEntryId);
        Assert.Equal(2, created.SourceLineNumber);
        Assert.Equal("USD", created.Currency);
        Assert.Equal("Financing", created.Activity);

        var history = await service.GetByJournalEntryAsync(
            bookId, journalEntryId, CancellationToken.None);
        Assert.Single(history);

        await Assert.ThrowsAsync<CashFlowNonCashDisclosureAlreadyExistsException>(() =>
            service.CreateAsync(
                bookId, journalEntryId, 2,
                new CreateCashFlowNonCashDisclosureInput(
                    125_000m, "USD", CashFlowActivity.Financing,
                    "ASSET_ACQUIRED_THROUGH_LEASE", null),
                CancellationToken.None));

        await Assert.ThrowsAsync<CashFlowNonCashDisclosureSourceInvalidException>(() =>
            service.CreateAsync(
                bookId, cashEntry.Id, 1,
                new CreateCashFlowNonCashDisclosureInput(
                    25m, "USD", CashFlowActivity.Financing, "CASH_RECEIPT", null),
                CancellationToken.None));

        var reportReadRepository = scope.ServiceProvider.GetRequiredService<ICashFlowStatementReadRepository>();
        var reportSnapshot = await reportReadRepository.GetBeforeAsync(
            bookId, now.AddDays(1), CancellationToken.None);

        var reportDisclosure = Assert.Single(reportSnapshot.NonCashDisclosures);
        Assert.Equal(created.Id, reportDisclosure.Id);
        Assert.Equal(125_000m, reportDisclosure.Amount);
        Assert.Equal(CashFlowActivity.Financing, reportDisclosure.Activity);
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
