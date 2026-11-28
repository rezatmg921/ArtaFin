using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests.Domain.Finance;

public sealed class AggregateFieldLengthTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Account_RejectsCodeLongerThanPersistenceLimit()
    {
        Assert.Throws<ArgumentException>(() =>
            Account.Create(Guid.NewGuid(), Guid.NewGuid(), new string('A', 51), "Cash", AccountType.Asset, Now));
    }

    [Fact]
    public void Account_RenameRejectsNameLongerThanPersistenceLimit()
    {
        var account = Account.Create(Guid.NewGuid(), Guid.NewGuid(), "1000", "Cash", AccountType.Asset, Now);
        Assert.Throws<ArgumentException>(() => account.Rename(new string('N', 201)));
    }

    [Fact]
    public void FinancialBook_RejectsNameLongerThanPersistenceLimit()
    {
        Assert.Throws<ArgumentException>(() =>
            FinancialBook.Create(Guid.NewGuid(), Guid.NewGuid(), new string('B', 201), "USD", Now));
    }

    [Fact]
    public void JournalEntry_RejectsDescriptionLongerThanPersistenceLimit()
    {
        var lines = new[]
        {
            new JournalLine(Guid.NewGuid(), PostingSide.Debit, new Money(1m, "USD")),
            new JournalLine(Guid.NewGuid(), PostingSide.Credit, new Money(1m, "USD"))
        };

        Assert.Throws<ArgumentException>(() =>
            JournalEntry.Post(Guid.NewGuid(), Guid.NewGuid(), Now, new string('D', 501), lines));
    }
}
