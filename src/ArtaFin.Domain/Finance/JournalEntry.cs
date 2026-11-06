using ArtaFin.Domain.Common;

namespace ArtaFin.Domain.Finance;

public enum PostingSide
{
    Debit = 1,
    Credit = 2
}

/// <summary>A line in a balanced journal entry. Amount is always positive.</summary>
public sealed record JournalLine
{
    public Guid AccountId { get; }
    public PostingSide Side { get; }
    public Money Amount { get; }

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
        Amount = amount;
    }
}

/// <summary>
/// Immutable posted accounting record. Initial policy: a journal entry has one currency;
/// FX requires a dedicated balanced conversion policy rather than implicit arithmetic.
/// </summary>
public sealed class JournalEntry : Entity
{
    private readonly IReadOnlyList<JournalLine> _lines;

    public Guid FinancialBookId { get; }
    public DateTimeOffset PostedAt { get; }
    public string Description { get; }
    public IReadOnlyList<JournalLine> Lines => _lines;

    private JournalEntry(Guid id, Guid financialBookId, DateTimeOffset postedAt,
        string description, IReadOnlyList<JournalLine> lines) : base(id)
    {
        FinancialBookId = financialBookId;
        PostedAt = postedAt;
        Description = description;
        _lines = lines;
    }

    public static JournalEntry Post(Guid id, Guid financialBookId, DateTimeOffset postedAt,
        string description, IEnumerable<JournalLine> lines)
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

        return new JournalEntry(id, financialBookId, postedAt, description.Trim(),
            Array.AsReadOnly(snapshot));
    }
}
