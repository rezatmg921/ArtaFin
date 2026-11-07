using ArtaFin.Domain.Common;

namespace ArtaFin.Domain.Finance;

public enum PostingSide
{
    Debit = 1,
    Credit = 2
}

/// <summary>
/// A journal line supplied to the posting operation. The owning JournalEntry assigns
/// its stable line number when the entry is posted.
/// </summary>
public sealed record JournalLine
{
    // EF Core materializes the scalar backing fields through this constructor.
    private JournalLine()
    {
    }

    private decimal _amount;
    private string _currency = null!;

    public int LineNumber { get; private set; }
    public Guid AccountId { get; private set; }
    public PostingSide Side { get; private set; }

    // Keep Money as the Domain-facing value object while persistence stores its
    // amount and currency as separate scalar columns.
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

    internal static JournalLine CreateNumbered(JournalLine source, int lineNumber)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (lineNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(lineNumber));

        return new JournalLine(source.AccountId, source.Side, source.Amount)
        {
            LineNumber = lineNumber
        };
    }
}

/// <summary>
/// Immutable posted accounting record. Initial policy: a journal entry has one currency;
/// FX requires a dedicated balanced conversion policy rather than implicit arithmetic.
/// </summary>
public sealed class JournalEntry : AggregateRoot
{
    private readonly List<JournalLine> _lines = [];

    public Guid FinancialBookId { get; }
    public DateTimeOffset PostedAt { get; }
    public string Description { get; }

    /// <summary>
    /// Exposes a read-only view. EF Core populates the private backing field.
    /// </summary>
    public IReadOnlyList<JournalLine> Lines => _lines.AsReadOnly();

    // EF Core binds the scalar constructor parameters and populates _lines separately.
    private JournalEntry(
        Guid id,
        Guid financialBookId,
        DateTimeOffset postedAt,
        string description) : base(id)
    {
        FinancialBookId = financialBookId;
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

        var entry = new JournalEntry(
            id,
            financialBookId,
            postedAt,
            description.Trim());

        for (var index = 0; index < snapshot.Length; index++)
            entry._lines.Add(JournalLine.CreateNumbered(snapshot[index], index + 1));

        return entry;
    }
}
