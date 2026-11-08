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
    [Fact]
    public void Reverse_CreatesANewEntryWithOppositeSidesAndReferenceToOriginal()
    {
        var originalId = Guid.NewGuid();
        var original = JournalEntry.Post(
            originalId, BookId, DateTimeOffset.UtcNow, "Original entry",
            [
                new JournalLine(ExpenseAccount, PostingSide.Debit, new Money(12m, "EUR")),
                new JournalLine(CashAccount, PostingSide.Credit, new Money(12m, "EUR"))
            ]);

        var reversal = JournalEntry.Reverse(
            Guid.NewGuid(), original, DateTimeOffset.UtcNow, "Reverse original entry");

        Assert.Equal(originalId, reversal.ReversesJournalEntryId);
        Assert.Equal(original.FinancialBookId, reversal.FinancialBookId);
        Assert.Equal(2, reversal.Lines.Count);
        Assert.Equal(ExpenseAccount, reversal.Lines[0].AccountId);
        Assert.Equal(PostingSide.Credit, reversal.Lines[0].Side);
        Assert.Equal(12m, reversal.Lines[0].Amount.Amount);
        Assert.Equal(CashAccount, reversal.Lines[1].AccountId);
        Assert.Equal(PostingSide.Debit, reversal.Lines[1].Side);
        Assert.Equal(12m, reversal.Lines[1].Amount.Amount);
    }

    [Fact]
    public void Reverse_RejectsAnEntryThatIsAlreadyAReversal()
    {
        var original = JournalEntry.Post(
            Guid.NewGuid(), BookId, DateTimeOffset.UtcNow, "Original entry",
            [
                new JournalLine(ExpenseAccount, PostingSide.Debit, new Money(12m, "EUR")),
                new JournalLine(CashAccount, PostingSide.Credit, new Money(12m, "EUR"))
            ]);
        var reversal = JournalEntry.Reverse(
            Guid.NewGuid(), original, DateTimeOffset.UtcNow, "Reversal");

        Assert.Throws<DomainException>(() =>
            JournalEntry.Reverse(
                Guid.NewGuid(), reversal, DateTimeOffset.UtcNow, "Reverse the reversal"));
    }

    [Fact]
    public void Reverse_RejectsSelfReversal()
    {
        var originalId = Guid.NewGuid();
        var original = JournalEntry.Post(
            originalId, BookId, DateTimeOffset.UtcNow, "Original entry",
            [
                new JournalLine(ExpenseAccount, PostingSide.Debit, new Money(12m, "EUR")),
                new JournalLine(CashAccount, PostingSide.Credit, new Money(12m, "EUR"))
            ]);

        Assert.Throws<DomainException>(() =>
            JournalEntry.Reverse(
                originalId, original, DateTimeOffset.UtcNow, "Self reversal"));
    }

    [Fact]
    public void Reverse_DoesNotMutateOriginalEntry()
    {
        var original = JournalEntry.Post(
            Guid.NewGuid(), BookId, DateTimeOffset.UtcNow, "Original entry",
            [
                new JournalLine(ExpenseAccount, PostingSide.Debit, new Money(12m, "EUR")),
                new JournalLine(CashAccount, PostingSide.Credit, new Money(12m, "EUR"))
            ]);

        var originalDebitSide = original.Lines[0].Side;
        var originalCreditSide = original.Lines[1].Side;

        _ = JournalEntry.Reverse(
            Guid.NewGuid(), original, DateTimeOffset.UtcNow, "Reversal");

        Assert.Equal(originalDebitSide, original.Lines[0].Side);
        Assert.Equal(originalCreditSide, original.Lines[1].Side);
        Assert.Null(original.ReversesJournalEntryId);
    }

}
