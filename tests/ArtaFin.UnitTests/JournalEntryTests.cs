using ArtaFin.Domain.Common;
using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests;

public sealed class JournalEntryTests
{
    private static readonly Guid BookId = Guid.NewGuid();
    private static readonly Guid ExpenseAccount = Guid.NewGuid();
    private static readonly Guid CashAccount = Guid.NewGuid();

    [Fact]
    public void Post_AcceptsBalancedLinesAndAssignsStableLineNumbers()
    {
        var entry = JournalEntry.Post(Guid.NewGuid(), BookId, DateTimeOffset.UtcNow, "Lunch",
        [
            new JournalLine(ExpenseAccount, PostingSide.Debit, new Money(12.50m, "EUR")),
            new JournalLine(CashAccount, PostingSide.Credit, new Money(12.50m, "EUR"))
        ]);

        Assert.Equal(2, entry.Lines.Count);
        Assert.Equal("Lunch", entry.Description);
        Assert.Equal([1, 2], entry.Lines.Select(line => line.LineNumber));
    }

    [Fact]
    public void Post_RejectsUnbalancedLines()
    {
        Assert.Throws<DomainException>(() => JournalEntry.Post(Guid.NewGuid(), BookId,
            DateTimeOffset.UtcNow, "Lunch",
        [
            new JournalLine(ExpenseAccount, PostingSide.Debit, new Money(12m, "EUR")),
            new JournalLine(CashAccount, PostingSide.Credit, new Money(11m, "EUR"))
        ]));
    }

    [Fact]
    public void Post_RejectsMixedCurrencies()
    {
        Assert.Throws<DomainException>(() => JournalEntry.Post(Guid.NewGuid(), BookId,
            DateTimeOffset.UtcNow, "Exchange",
        [
            new JournalLine(ExpenseAccount, PostingSide.Debit, new Money(12m, "EUR")),
            new JournalLine(CashAccount, PostingSide.Credit, new Money(12m, "USD"))
        ]));
    }

    [Fact]
    public void JournalLines_AreSnapshotAndReadOnly()
    {
        var lines = new List<JournalLine>
        {
            new(ExpenseAccount, PostingSide.Debit, new Money(8m, "EUR")),
            new(CashAccount, PostingSide.Credit, new Money(8m, "EUR"))
        };

        var entry = JournalEntry.Post(Guid.NewGuid(), BookId, DateTimeOffset.UtcNow, "Coffee", lines);
        lines.Clear();

        Assert.Equal(2, entry.Lines.Count);
        Assert.Equal([1, 2], entry.Lines.Select(line => line.LineNumber));

        var exposedCollection = Assert.IsAssignableFrom<IList<JournalLine>>(entry.Lines);
        Assert.Throws<NotSupportedException>(() => exposedCollection.Add(
            new JournalLine(ExpenseAccount, PostingSide.Debit, new Money(1m, "EUR"))));
    }

    [Fact]
    public void Post_DoesNotMutateInputLinesWhenAssigningNumbers()
    {
        var first = new JournalLine(ExpenseAccount, PostingSide.Debit, new Money(8m, "EUR"));
        var second = new JournalLine(CashAccount, PostingSide.Credit, new Money(8m, "EUR"));

        var entry = JournalEntry.Post(Guid.NewGuid(), BookId, DateTimeOffset.UtcNow, "Coffee",
            [first, second]);

        Assert.Equal(0, first.LineNumber);
        Assert.Equal(0, second.LineNumber);
        Assert.Equal([1, 2], entry.Lines.Select(line => line.LineNumber));
    }
}
