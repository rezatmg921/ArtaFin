using ArtaFin.Domain.Common;
using ArtaFin.Domain.Finance;

namespace ArtaFin.Domain.Obligations;

/// <summary>
/// A book-scoped amount owed either by a counterparty to the book or by the book
/// to a counterparty. The aggregate owns an immutable initial installment schedule.
/// </summary>
public sealed class FinancialObligation : AggregateRoot
{
    public const int MaxDescriptionLength = 500;
    public const int MaxReferenceLength = 100;
    public const int MaxInstallments = 1200;

    private readonly List<FinancialObligationInstallment> _installments = [];

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
    public IReadOnlyList<FinancialObligationInstallment> Installments => _installments.AsReadOnly();

    private FinancialObligation() : base(Guid.NewGuid()) { }

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
        DateTimeOffset createdAt,
        IReadOnlyList<FinancialObligationInstallmentTerm> installments) : base(id)
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
        for (var index = 0; index < installments.Count; index++)
        {
            var term = installments[index];
            _installments.Add(new FinancialObligationInstallment(
                Guid.NewGuid(), Id, index + 1, term.DueDate, term.Amount));
        }
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
        DateTimeOffset createdAt,
        IReadOnlyList<FinancialObligationInstallmentTerm>? installments = null)
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

        var normalizedInstallments = NormalizeInstallments(installments, money, effectiveDate, dueDate);
        return new FinancialObligation(
            id, financialBookId, contactId, controlAccountId, direction, kind,
            money.Amount, money.Currency, effectiveDate, dueDate, normalizedDescription, normalizedReference,
            createdAt, normalizedInstallments);
    }

    private static IReadOnlyList<FinancialObligationInstallmentTerm> NormalizeInstallments(
        IReadOnlyList<FinancialObligationInstallmentTerm>? installments,
        Money total,
        DateOnly effectiveDate,
        DateOnly dueDate)
    {
        if (installments is null || installments.Count == 0)
            return [new FinancialObligationInstallmentTerm(total.Amount, dueDate)];

        if (installments.Count > MaxInstallments)
            throw new ArgumentOutOfRangeException(nameof(installments),
                $"An obligation cannot have more than {MaxInstallments} installments.");

        decimal sum = 0m;
        DateOnly? previousDate = null;
        var normalized = new List<FinancialObligationInstallmentTerm>(installments.Count);
        foreach (var installment in installments)
        {
            if (installment.Amount <= 0m)
                throw new ArgumentOutOfRangeException(nameof(installments), "Installment amounts must be greater than zero.");
            var installmentMoney = new Money(installment.Amount, total.Currency);
            if (installment.DueDate < effectiveDate || installment.DueDate > dueDate)
                throw new ArgumentException("Installment due dates must be between the effective date and obligation due date.", nameof(installments));
            if (previousDate.HasValue && installment.DueDate <= previousDate.Value)
                throw new ArgumentException("Installment due dates must be strictly increasing.", nameof(installments));
            if (installmentMoney.Amount > total.Amount - sum)
                throw new ArgumentException("The sum of installment amounts cannot exceed the original obligation amount.", nameof(installments));
            sum += installmentMoney.Amount;
            previousDate = installment.DueDate;
            normalized.Add(new FinancialObligationInstallmentTerm(installmentMoney.Amount, installment.DueDate));
        }

        if (sum != total.Amount)
            throw new ArgumentException("The sum of installment amounts must equal the original obligation amount.", nameof(installments));
        if (normalized[^1].DueDate != dueDate)
            throw new ArgumentException("The final installment due date must equal the obligation due date.", nameof(installments));

        return normalized;
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

    /// <summary>
    /// Refreshes settlement status from the cumulative amount allocated by immutable payments.
    /// The caller must calculate the amount inside the payment-posting transaction.
    /// </summary>
    public void ApplyPayment(decimal cumulativePaidAmount, DateTimeOffset changedAt)
    {
        if (Status == FinancialObligationStatus.Cancelled)
            throw new DomainException("A cancelled obligation cannot receive payments.");
        if (Status == FinancialObligationStatus.Settled && cumulativePaidAmount != OriginalAmount)
            throw new DomainException("A settled obligation cannot return to a partially settled state.");
        if (cumulativePaidAmount <= 0m || cumulativePaidAmount > OriginalAmount)
            throw new DomainException("Cumulative payments must be greater than zero and cannot exceed the obligation amount.");

        var paidMoney = new Money(cumulativePaidAmount, Currency);
        EnsureNotBeforeLastChange(changedAt);

        Status = paidMoney.Amount == OriginalAmount
            ? FinancialObligationStatus.Settled
            : FinancialObligationStatus.PartiallySettled;
        UpdatedAt = changedAt;
    }

    /// <summary>Recomputes settlement state after a payment reversal.</summary>
    public void RecalculateSettlementAfterReversal(decimal cumulativePaidAmount, DateTimeOffset changedAt)
    {
        if (Status == FinancialObligationStatus.Cancelled)
            throw new DomainException("A cancelled obligation cannot have its payment history reversed.");
        if (cumulativePaidAmount < 0m || cumulativePaidAmount > OriginalAmount)
            throw new DomainException("Cumulative payments cannot be negative or exceed the obligation amount.");

        var paidMoney = new Money(cumulativePaidAmount, Currency);
        EnsureNotBeforeLastChange(changedAt);
        Status = paidMoney.Amount == 0m
            ? FinancialObligationStatus.Open
            : paidMoney.Amount == OriginalAmount
                ? FinancialObligationStatus.Settled
                : FinancialObligationStatus.PartiallySettled;
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


/// <summary>Immutable contractual terms for one installment of an obligation.</summary>
public sealed class FinancialObligationInstallment : Entity
{
    private FinancialObligationInstallment() : base(Guid.NewGuid()) { }

    internal FinancialObligationInstallment(
        Guid id, Guid financialObligationId, int installmentNumber, DateOnly dueDate, decimal amount) : base(id)
    {
        FinancialObligationId = financialObligationId;
        InstallmentNumber = installmentNumber;
        DueDate = dueDate;
        Amount = amount;
    }

    public Guid FinancialObligationId { get; private set; }
    public int InstallmentNumber { get; private set; }
    public DateOnly DueDate { get; private set; }
    public decimal Amount { get; private set; }
}

/// <summary>A proposed installment amount and due date supplied when creating an obligation.</summary>
public sealed record FinancialObligationInstallmentTerm(decimal Amount, DateOnly DueDate);
