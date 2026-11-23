using ArtaFin.Domain.Contacts;
using ArtaFin.Domain.Finance;
using ArtaFin.Domain.Obligations;
using ArtaFin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArtaFin.Infrastructure.IntegrationTests;

public sealed class FinancialObligationPaymentPersistenceTests : IAsyncLifetime
{
    private readonly DbContextOptions<ArtaFinDbContext> _options;

    public FinancialObligationPaymentPersistenceTests()
    {
        _options = new DbContextOptionsBuilder<ArtaFinDbContext>()
            .UseSqlServer(IntegrationTestDatabase.GetConnectionString())
            .Options;
    }

    public async Task InitializeAsync()
    {
        await using var context = new ArtaFinDbContext(_options);
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SaveAndReload_PreservesPaymentAllocationsAndJournalLink()
    {
        var now = new DateTimeOffset(2026, 11, 23, 10, 0, 0, TimeSpan.Zero);
        var bookId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var controlAccountId = Guid.NewGuid();
        var cashAccountId = Guid.NewGuid();
        var obligationId = Guid.NewGuid();
        var installmentId = Guid.NewGuid();
        var journalEntryId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var installmentDueDate = new DateOnly(2026, 12, 1);

        var book = FinancialBook.Create(bookId, ownerId, "Payment persistence", "USD", now);
        var contact = Contact.Create(contactId, bookId, ContactType.Person, "Customer", null, null, null, now);
        var controlAccount = Account.Create(controlAccountId, bookId, "1100", "Accounts receivable", AccountType.Asset, now);
        var cashAccount = Account.Create(cashAccountId, bookId, "1000", "Bank", AccountType.Asset, now);
        var obligation = FinancialObligation.Create(
            obligationId, bookId, contactId, controlAccountId,
            FinancialObligationDirection.Receivable, FinancialObligationKind.GeneralDebt,
            125m, "USD", new DateOnly(2026, 11, 1), installmentDueDate,
            "Customer invoice", null, now,
            [new FinancialObligationInstallmentTerm(125m, installmentDueDate)]);
        var installment = Assert.Single(obligation.Installments);
        installmentId = installment.Id;

        var journalEntry = JournalEntry.Post(journalEntryId, bookId, now, "Customer payment",
        [
            new JournalLine(cashAccountId, PostingSide.Debit, new Money(125m, "USD")),
            new JournalLine(controlAccountId, PostingSide.Credit, new Money(125m, "USD"))
        ]);
        var payment = FinancialObligationPayment.Create(
            paymentId, bookId, obligationId, cashAccountId, journalEntryId,
            125m, "USD", new DateOnly(2026, 11, 23), "Customer payment",
            "RCPT-125", ownerId, now, "payment-persistence-001", new string('B', 64),
            [new FinancialObligationPaymentAllocationTerm(installmentId, 125m)]);
        obligation.ApplyPayment(125m, now);

        await using (var write = new ArtaFinDbContext(_options))
        {
            write.FinancialBooks.Add(book);
            write.Contacts.Add(contact);
            write.Accounts.AddRange(controlAccount, cashAccount);
            write.FinancialObligations.Add(obligation);
            write.JournalEntries.Add(journalEntry);
            write.FinancialObligationPayments.Add(payment);
            await write.SaveChangesAsync();
        }

        await using var read = new ArtaFinDbContext(_options);
        var persisted = await read.FinancialObligationPayments.AsNoTracking()
            .Include(x => x.Allocations)
            .SingleAsync(x => x.Id == paymentId);

        Assert.Equal(bookId, persisted.FinancialBookId);
        Assert.Equal(obligationId, persisted.FinancialObligationId);
        Assert.Equal(cashAccountId, persisted.CashAccountId);
        Assert.Equal(journalEntryId, persisted.JournalEntryId);
        Assert.Equal(125m, persisted.Amount);
        Assert.Equal("USD", persisted.Currency);
        Assert.Equal("RCPT-125", persisted.Reference);
        var allocation = Assert.Single(persisted.Allocations);
        Assert.Equal(installmentId, allocation.InstallmentId);
        Assert.Equal(125m, allocation.Amount);

        var persistedObligation = await read.FinancialObligations.SingleAsync(x => x.Id == obligationId);
        Assert.Equal(FinancialObligationStatus.Settled, persistedObligation.Status);
    }

    [Fact]
    public async Task SaveChanges_RejectsDeletingPostedPaymentAllocation()
    {
        var now = new DateTimeOffset(2026, 11, 23, 10, 0, 0, TimeSpan.Zero);
        var bookId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var controlAccountId = Guid.NewGuid();
        var cashAccountId = Guid.NewGuid();
        var obligationId = Guid.NewGuid();
        var journalEntryId = Guid.NewGuid();
        var installmentId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var dueDate = new DateOnly(2026, 12, 1);

        var book = FinancialBook.Create(bookId, ownerId, "Immutable payment", "USD", now);
        var contact = Contact.Create(contactId, bookId, ContactType.Person, "Customer", null, null, null, now);
        var control = Account.Create(controlAccountId, bookId, "1100", "Receivable", AccountType.Asset, now);
        var cash = Account.Create(cashAccountId, bookId, "1000", "Bank", AccountType.Asset, now);
        var obligation = FinancialObligation.Create(
            obligationId, bookId, contactId, controlAccountId,
            FinancialObligationDirection.Receivable, FinancialObligationKind.GeneralDebt,
            50m, "USD", new DateOnly(2026, 11, 1), dueDate, "Invoice", null, now,
            [new FinancialObligationInstallmentTerm(50m, dueDate)]);
        installmentId = Assert.Single(obligation.Installments).Id;
        var entry = JournalEntry.Post(journalEntryId, bookId, now, "Receipt",
        [
            new JournalLine(cashAccountId, PostingSide.Debit, new Money(50m, "USD")),
            new JournalLine(controlAccountId, PostingSide.Credit, new Money(50m, "USD"))
        ]);
        var payment = FinancialObligationPayment.Create(
            paymentId, bookId, obligationId, cashAccountId, journalEntryId,
            50m, "USD", new DateOnly(2026, 11, 23), "Receipt", null,
            ownerId, now, "payment-immutable-001", new string('C', 64),
            [new FinancialObligationPaymentAllocationTerm(installmentId, 50m)]);

        await using (var write = new ArtaFinDbContext(_options))
        {
            write.FinancialBooks.Add(book);
            write.Contacts.Add(contact);
            write.Accounts.AddRange(control, cash);
            write.FinancialObligations.Add(obligation);
            write.JournalEntries.Add(entry);
            write.FinancialObligationPayments.Add(payment);
            await write.SaveChangesAsync();
        }

        await using var update = new ArtaFinDbContext(_options);
        var allocation = await update.FinancialObligationPaymentAllocations
            .SingleAsync(x => x.PaymentId == paymentId);
        update.FinancialObligationPaymentAllocations.Remove(allocation);

        await Assert.ThrowsAsync<InvalidOperationException>(() => update.SaveChangesAsync());
    }
}
