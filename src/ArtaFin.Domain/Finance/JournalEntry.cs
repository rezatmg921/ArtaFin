using ArtaFin.Domain.Common;

namespace ArtaFin.Domain.Finance;

public enum PostingSide
{
    Debit = 1,
    Credit = 2
    /// <summary>
    /// Creates a new posted entry that reverses the accounting effect of an original entry.
    /// The original entry remains unchanged.
    /// </summary>
    public static JournalEntry Reverse(
        Guid id,
        JournalEntry original,
        DateTimeOffset postedAt,
        string description)
    {
        ArgumentNullException.ThrowIfNull(original);

        if (id == Guid.Empty)
            throw new ArgumentException("Journal entry id cannot be empty.", nameof(id));
        if (id == original.Id)
            throw new DomainException("A journal entry cannot reverse itself.");
        if (original.ReversesJournalEntryId.HasValue)
            throw new DomainException("A reversal entry cannot be reversed.");

        var reversal = new JournalEntry(
            id,
            original.FinancialBookId,
            postedAt,
            description,
            original.Id);

        foreach (var line in original.Lines)
        {
            var oppositeSide = line.Side == PostingSide.Debit
                ? PostingSide.Credit
                : PostingSide.Debit;

            reversal._lines.Add(JournalLine.CreateNumbered(
                new JournalLine(line.AccountId, oppositeSide, line.Amount),
                line.LineNumber,
                original.FinancialBookId));
        }

        // Reuse Post's validation rules for description, balance, currency and line count.
        _ = Post(Guid.NewGuid(), original.FinancialBookId, postedAt, description,
            reversal._lines.Select(line =>
                new JournalLine(line.AccountId, line.Side, line.Amount)));

        return reversal;
    }

}

/// <summary>
/// A journal line supplied to the posting operation. The owning JournalEntry assigns
/// its stable line number and FinancialBookId when the entry is posted.
/// </summary>
public sealed record JournalLine
{
    private JournalLine()
    {
    }

    private decimal _amount;
    private string _currency = null!;

    public int LineNumber { get; private set; }
    public Guid FinancialBookId { get; private set; }
    public Guid AccountId { get; private set; }
    public PostingSide Side { get; private set; }
    public Money Amount => new(_amount, _currency);

    public JournalLine(Guid accountId, PostingSide side, Money amount)
    {
        if (accountId == Guid.Empty)
            throw new ArgumentException("Account id cannot be empty.", nameof(accountId));
        if (side is not PostingSide.Debit and not PostingSide.Credit)
            throw new ArgumentOutOfRangeException(nameof(side));
        if (amount.Amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Journal line amount must be positive.");

        AccountId = accountId;
        Side = side;
        _amount = amount.Amount;
        _currency = amount.Currency;
    }

    internal static JournalLine CreateNumbered(JournalLine source, int lineNumber, Guid financialBookId)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (lineNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(lineNumber));
        if (financialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(financialBookId));

        return new JournalLine(source.AccountId, source.Side, source.Amount)
        {
            LineNumber = lineNumber,
            FinancialBookId = financialBookId
        };
    }
}

/// <summary>
/// Immutable posted accounting record. Initial policy: a journal entry has one currency;
/// FX requires a dedicated balanced conversion policy rather than implicit arithmetic.
/// </summary>
public sealed class JournalEntry : AggregateRoot
{
    public const int MaxDescriptionLength = 500;

    private readonly List<JournalLine> _lines = [];

    public Guid FinancialBookId { get; }
    public Guid? ReversesJournalEntryId { get; }
    public DateTimeOffset PostedAt { get; }
    public string Description { get; }
    public IReadOnlyList<JournalLine> Lines => _lines.AsReadOnly();

    private JournalEntry(
        Guid id,
        Guid financialBookId,
        DateTimeOffset postedAt,
        string description,
        Guid? reversesJournalEntryId = null) : base(id)
    {
        FinancialBookId = financialBookId;
        ReversesJournalEntryId = reversesJournalEntryId;
        PostedAt = postedAt;
        Description = description;
    }

    public static JournalEntry Post(
        Guid id,
        Guid financialBookId,
        DateTimeOffset postedAt,
        string description,
        IEnumerable<JournalLine> lines)
    {
        if (financialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(financialBookId));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));

        var normalizedDescription = description.Trim();
        if (normalizedDescription.Length > MaxDescriptionLength)
            throw new ArgumentException($"Description cannot exceed {MaxDescriptionLength} characters.", nameof(description));

        ArgumentNullException.ThrowIfNull(lines);
        var snapshot = lines.ToArray();
        if (snapshot.Length < 2)
            throw new DomainException("A journal entry requires at least two lines.");

        var currency = snapshot[0].Amount.Currency;
        if (snapshot.Any(line => !StringComparer.Ordinal.Equals(line.Amount.Currency, currency)))
            throw new DomainException("All journal lines must use the same currency.");

        var debits = snapshot.Where(x => x.Side == PostingSide.Debit).Sum(x => x.Amount.Amount);
        var credits = snapshot.Where(x => x.Side == PostingSide.Credit).Sum(x => x.Amount.Amount);
        if (debits != credits)
            throw new DomainException("Journal entry debits must equal credits.");

        var entry = new JournalEntry(id, financialBookId, postedAt, normalizedDescription);
        for (var index = 0; index < snapshot.Length; index++)
            entry._lines.Add(JournalLine.CreateNumbered(snapshot[index], index + 1, financialBookId));

        return entry;
    }
}
