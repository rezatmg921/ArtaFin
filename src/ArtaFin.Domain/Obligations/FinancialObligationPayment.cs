using ArtaFin.Domain.Common;
using ArtaFin.Domain.Finance;

namespace ArtaFin.Domain.Obligations;

/// <summary>
/// An immutable posted receipt or disbursement against one financial obligation.
/// The linked journal entry is the accounting evidence; allocations are the settlement evidence.
/// </summary>
public sealed class FinancialObligationPayment : AggregateRoot
{
    public const int MaxDescriptionLength = 500;
    public const int MaxReferenceLength = 100;
    public const int MaxAllocations = FinancialObligation.MaxInstallments;

    private readonly List<FinancialObligationPaymentAllocation> _allocations = [];

    public Guid FinancialBookId { get; private set; }
    public Guid FinancialObligationId { get; private set; }
    public Guid CashAccountId { get; private set; }
    public Guid JournalEntryId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = null!;
    public Money PaymentMoney => new(Amount, Currency);
    public DateOnly PaymentDate { get; private set; }
    public string Description { get; private set; } = null!;
    public string? Reference { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string IdempotencyKey { get; private set; } = null!;
    public string RequestHash { get; private set; } = null!;
    public IReadOnlyList<FinancialObligationPaymentAllocation> Allocations => _allocations.AsReadOnly();

    private FinancialObligationPayment() : base(Guid.NewGuid()) { }

    private FinancialObligationPayment(
        Guid id, Guid financialBookId, Guid financialObligationId, Guid cashAccountId,
        Guid journalEntryId, Money amount, DateOnly paymentDate, string description,
        string? reference, Guid createdBy, DateTimeOffset createdAt,
        string idempotencyKey, string requestHash,
        IReadOnlyList<FinancialObligationPaymentAllocationTerm> allocations) : base(id)
    {
        FinancialBookId = financialBookId;
        FinancialObligationId = financialObligationId;
        CashAccountId = cashAccountId;
        JournalEntryId = journalEntryId;
        Amount = amount.Amount;
        Currency = amount.Currency;
        PaymentDate = paymentDate;
        Description = description;
        Reference = reference;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        IdempotencyKey = idempotencyKey;
        RequestHash = requestHash;

        foreach (var allocation in allocations)
            _allocations.Add(new FinancialObligationPaymentAllocation(
                Guid.NewGuid(), Id, financialObligationId, allocation.InstallmentId,
                allocation.Amount));
    }

    public static FinancialObligationPayment Create(
        Guid id, Guid financialBookId, Guid financialObligationId, Guid cashAccountId,
        Guid journalEntryId, decimal amount, string currency, DateOnly paymentDate,
        string description, string? reference, Guid createdBy, DateTimeOffset createdAt,
        string idempotencyKey, string requestHash,
        IReadOnlyList<FinancialObligationPaymentAllocationTerm> allocations)
    {
        if (financialBookId == Guid.Empty) throw new ArgumentException("Financial book id cannot be empty.", nameof(financialBookId));
        if (financialObligationId == Guid.Empty) throw new ArgumentException("Financial obligation id cannot be empty.", nameof(financialObligationId));
        if (cashAccountId == Guid.Empty) throw new ArgumentException("Cash account id cannot be empty.", nameof(cashAccountId));
        if (journalEntryId == Guid.Empty) throw new ArgumentException("Journal entry id cannot be empty.", nameof(journalEntryId));
        if (createdBy == Guid.Empty) throw new ArgumentException("Creator id cannot be empty.", nameof(createdBy));
        if (amount <= 0m) throw new ArgumentOutOfRangeException(nameof(amount), "Payment amount must be greater than zero.");
        var money = new Money(amount, currency);

        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Description is required.", nameof(description));
        var normalizedDescription = description.Trim();
        if (normalizedDescription.Length > MaxDescriptionLength)
            throw new ArgumentException($"Description cannot exceed {MaxDescriptionLength} characters.", nameof(description));
        var normalizedReference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        if (normalizedReference?.Length > MaxReferenceLength)
            throw new ArgumentException($"Reference cannot exceed {MaxReferenceLength} characters.", nameof(reference));

        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 128 || idempotencyKey.Any(char.IsControl))
            throw new ArgumentException("A valid idempotency key is required.", nameof(idempotencyKey));
        if (requestHash is null || requestHash.Length != 64 || !requestHash.All(Uri.IsHexDigit))
            throw new ArgumentException("A valid SHA-256 request hash is required.", nameof(requestHash));

        ArgumentNullException.ThrowIfNull(allocations);
        if (allocations.Count is < 1 or > MaxAllocations)
            throw new ArgumentOutOfRangeException(nameof(allocations), $"A payment requires 1 to {MaxAllocations} allocations.");

        var seenInstallments = new HashSet<Guid>();
        decimal allocated = 0m;
        foreach (var allocation in allocations)
        {
            if (allocation.InstallmentId == Guid.Empty)
                throw new ArgumentException("An installment id is required for every allocation.", nameof(allocations));
            if (!seenInstallments.Add(allocation.InstallmentId))
                throw new ArgumentException("An installment can appear only once in a payment.", nameof(allocations));
            if (allocation.Amount <= 0m)
                throw new ArgumentOutOfRangeException(nameof(allocations), "Allocation amounts must be greater than zero.");
            var allocationMoney = new Money(allocation.Amount, money.Currency);
            if (allocationMoney.Amount > money.Amount - allocated)
                throw new ArgumentException("The sum of allocations cannot exceed the payment amount.", nameof(allocations));
            allocated += allocationMoney.Amount;
        }
        if (allocated != money.Amount)
            throw new ArgumentException("The sum of allocations must equal the payment amount.", nameof(allocations));

        return new FinancialObligationPayment(
            id, financialBookId, financialObligationId, cashAccountId, journalEntryId,
            money, paymentDate, normalizedDescription, normalizedReference,
            createdBy, createdAt, idempotencyKey, requestHash.ToUpperInvariant(), allocations);
    }
}

/// <summary>An immutable allocation of a payment to one contractual installment.</summary>
public sealed class FinancialObligationPaymentAllocation : Entity
{
    private FinancialObligationPaymentAllocation() : base(Guid.NewGuid()) { }

    internal FinancialObligationPaymentAllocation(
        Guid id, Guid paymentId, Guid financialObligationId, Guid installmentId, decimal amount) : base(id)
    {
        PaymentId = paymentId;
        FinancialObligationId = financialObligationId;
        InstallmentId = installmentId;
        Amount = amount;
    }

    public Guid PaymentId { get; private set; }
    public Guid FinancialObligationId { get; private set; }
    public Guid InstallmentId { get; private set; }
    public decimal Amount { get; private set; }
}

public sealed record FinancialObligationPaymentAllocationTerm(Guid InstallmentId, decimal Amount);
