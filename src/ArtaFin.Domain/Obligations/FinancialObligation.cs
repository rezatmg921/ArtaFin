using ArtaFin.Domain.Common;
using ArtaFin.Domain.Finance;

namespace ArtaFin.Domain.Obligations;

/// <summary>
/// A book-scoped amount owed either by a counterparty to the book or by the book
/// to a counterparty. Installments and immutable settlement records are separate
/// concepts and are introduced by subsequent capabilities.
/// </summary>
public sealed class FinancialObligation : AggregateRoot
{
    public const int MaxDescriptionLength = 500;
    public const int MaxReferenceLength = 100;

    public Guid FinancialBookId { get; private set; }
    public Guid ContactId { get; private set; }
    public Guid ControlAccountId { get; private set; }
    public FinancialObligationDirection Direction { get; private set; }
    public FinancialObligationKind Kind { get; private set; }
    public decimal OriginalAmount { get; private set; }
    public string Currency { get; private set; } = null!;
    public Money OriginalMoney => new(OriginalAmount, Currency);
    public DateOnly EffectiveDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public string Description { get; private set; } = null!;
    public string? Reference { get; private set; }
    public FinancialObligationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }

    private FinancialObligation(
        Guid id,
        Guid financialBookId,
        Guid contactId,
        Guid controlAccountId,
        FinancialObligationDirection direction,
        FinancialObligationKind kind,
        decimal originalAmount,
        string currency,
        DateOnly effectiveDate,
        DateOnly dueDate,
        string description,
        string? reference,
        DateTimeOffset createdAt) : base(id)
    {
        FinancialBookId = financialBookId;
        ContactId = contactId;
        ControlAccountId = controlAccountId;
        Direction = direction;
        Kind = kind;
        OriginalAmount = originalAmount;
        Currency = currency;
        EffectiveDate = effectiveDate;
        DueDate = dueDate;
        Description = description;
        Reference = reference;
        Status = FinancialObligationStatus.Open;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static FinancialObligation Create(
        Guid id,
        Guid financialBookId,
        Guid contactId,
        Guid controlAccountId,
        FinancialObligationDirection direction,
        FinancialObligationKind kind,
        decimal originalAmount,
        string currency,
        DateOnly effectiveDate,
        DateOnly dueDate,
        string description,
        string? reference,
        DateTimeOffset createdAt)
    {
        if (financialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(financialBookId));
        if (contactId == Guid.Empty)
            throw new ArgumentException("Contact id cannot be empty.", nameof(contactId));
        if (controlAccountId == Guid.Empty)
            throw new ArgumentException("Control account id cannot be empty.", nameof(controlAccountId));
        if (!Enum.IsDefined(direction))
            throw new ArgumentOutOfRangeException(nameof(direction));
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (originalAmount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(originalAmount), "Original amount must be greater than zero.");
        if (dueDate < effectiveDate)
            throw new ArgumentException("Due date cannot be earlier than the effective date.", nameof(dueDate));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));

        var normalizedDescription = description.Trim();
        if (normalizedDescription.Length > MaxDescriptionLength)
            throw new ArgumentException($"Description cannot exceed {MaxDescriptionLength} characters.", nameof(description));

        var normalizedReference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        if (normalizedReference?.Length > MaxReferenceLength)
            throw new ArgumentException($"Reference cannot exceed {MaxReferenceLength} characters.", nameof(reference));

        var money = new Money(originalAmount, currency);

        return new FinancialObligation(
            id, financialBookId, contactId, controlAccountId, direction, kind,
            money.Amount, money.Currency, effectiveDate, dueDate, normalizedDescription, normalizedReference, createdAt);
    }

    public bool IsOverdueOn(DateOnly date) =>
        Status is FinancialObligationStatus.Open or FinancialObligationStatus.PartiallySettled &&
        DueDate < date;

    public void MarkPartiallySettled(DateTimeOffset changedAt)
    {
        if (Status != FinancialObligationStatus.Open)
            throw new DomainException("Only an open obligation can become partially settled.");
        EnsureNotBeforeLastChange(changedAt);
        Status = FinancialObligationStatus.PartiallySettled;
        UpdatedAt = changedAt;
    }

    public void MarkSettled(DateTimeOffset settledAt)
    {
        if (Status is not FinancialObligationStatus.Open and not FinancialObligationStatus.PartiallySettled)
            throw new DomainException("Only an unsettled obligation can become settled.");
        EnsureNotBeforeLastChange(settledAt);
        Status = FinancialObligationStatus.Settled;
        UpdatedAt = settledAt;
    }

    public void Cancel(DateTimeOffset cancelledAt)
    {
        if (Status is not FinancialObligationStatus.Open)
            throw new DomainException("Only an open obligation can be cancelled.");
        EnsureNotBeforeLastChange(cancelledAt);
        Status = FinancialObligationStatus.Cancelled;
        CancelledAt = cancelledAt;
        UpdatedAt = cancelledAt;
    }

    private void EnsureNotBeforeLastChange(DateTimeOffset changedAt)
    {
        if (changedAt < UpdatedAt)
            throw new DomainException("Obligation change time cannot be earlier than its previous update.");
    }
}
