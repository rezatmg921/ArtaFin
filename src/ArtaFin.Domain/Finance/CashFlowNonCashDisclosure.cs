using ArtaFin.Domain.Common;

namespace ArtaFin.Domain.Finance;

/// <summary>
/// A separately disclosed non-cash investing or financing component linked
/// to one immutable posted journal line. It never contributes to cash totals.
/// </summary>
public sealed class CashFlowNonCashDisclosure : AggregateRoot
{
    public const int MaxCategoryLength = 64;
    public const int MaxMemoLength = 500;

    public Guid FinancialBookId { get; }
    public Guid JournalEntryId { get; }
    public int SourceLineNumber { get; }
    public decimal Amount { get; }
    public string Currency { get; }
    public CashFlowActivity Activity { get; }
    public string Category { get; }
    public string? Memo { get; }
    public Guid CreatedBy { get; }
    public DateTimeOffset CreatedAt { get; }

    private CashFlowNonCashDisclosure(
        Guid id,
        Guid financialBookId,
        Guid journalEntryId,
        int sourceLineNumber,
        decimal amount,
        string currency,
        CashFlowActivity activity,
        string category,
        string? memo,
        Guid createdBy,
        DateTimeOffset createdAt) : base(id)
    {
        FinancialBookId = financialBookId;
        JournalEntryId = journalEntryId;
        SourceLineNumber = sourceLineNumber;
        Amount = amount;
        Currency = currency;
        Activity = activity;
        Category = category;
        Memo = memo;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public static CashFlowNonCashDisclosure Create(
        Guid id,
        Guid financialBookId,
        Guid journalEntryId,
        int sourceLineNumber,
        decimal amount,
        string currency,
        CashFlowActivity activity,
        string category,
        string? memo,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || financialBookId == Guid.Empty ||
            journalEntryId == Guid.Empty || createdBy == Guid.Empty)
            throw new ArgumentException("Disclosure identifiers cannot be empty.");
        if (sourceLineNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(sourceLineNumber));
        if (amount <= 0 || decimal.Round(amount, 4) != amount)
            throw new DomainException("Disclosure amount must be positive and support at most four decimal places.");
        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3 ||
            !currency.Trim().All(char.IsAsciiLetter))
            throw new DomainException("Disclosure currency must be a three-letter currency code.");
        if (activity is not CashFlowActivity.Investing and not CashFlowActivity.Financing)
            throw new DomainException("Non-cash disclosures must be Investing or Financing activities.");
        if (string.IsNullOrWhiteSpace(category))
            throw new DomainException("Disclosure category is required.");

        var normalizedCategory = category.Trim();
        if (normalizedCategory.Length > MaxCategoryLength ||
            !char.IsAsciiLetter(normalizedCategory[0]) ||
            normalizedCategory.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character != '_'))
            throw new DomainException("Disclosure category must be a stable ASCII code.");

        var normalizedMemo = string.IsNullOrWhiteSpace(memo) ? null : memo.Trim();
        if (normalizedMemo?.Length > MaxMemoLength)
            throw new DomainException($"Memo cannot exceed {MaxMemoLength} characters.");

        return new CashFlowNonCashDisclosure(
            id, financialBookId, journalEntryId, sourceLineNumber, amount,
            currency.Trim().ToUpperInvariant(), activity, normalizedCategory,
            normalizedMemo, createdBy, createdAt);
    }
}
