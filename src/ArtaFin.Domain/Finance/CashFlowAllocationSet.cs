using ArtaFin.Domain.Common;

namespace ArtaFin.Domain.Finance;

public enum CashFlowActivity : byte
{
    Operating = 1,
    Investing = 2,
    Financing = 3
}

public enum CashFlowAllocationSetStatus : byte
{
    Draft = 1,
    Finalized = 2,
    Superseded = 3
}

/// <summary>
/// Auditable classification of one immutable cash/cash-equivalent journal line.
/// The source journal entry remains the accounting source of truth.
/// </summary>
public sealed class CashFlowAllocationSet : Entity
{
    public const int MaxCategoryLength = 64;
    public const int MaxMemoLength = 500;

    private readonly List<CashFlowAllocation> _allocations = [];

    private CashFlowAllocationSet(
        Guid id,
        Guid financialBookId,
        Guid sourceJournalEntryId,
        int sourceLineNumber,
        decimal sourceAmount,
        string currency,
        int version,
        Guid? supersedesAllocationSetId,
        Guid createdBy,
        DateTimeOffset createdAt) : base(id)
    {
        FinancialBookId = financialBookId;
        SourceJournalEntryId = sourceJournalEntryId;
        SourceLineNumber = sourceLineNumber;
        SourceAmount = sourceAmount;
        Currency = currency;
        Version = version;
        SupersedesAllocationSetId = supersedesAllocationSetId;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        Status = CashFlowAllocationSetStatus.Draft;
    }

    private CashFlowAllocationSet() : base(Guid.NewGuid()) { }

    public Guid FinancialBookId { get; private set; }
    public Guid SourceJournalEntryId { get; private set; }
    public int SourceLineNumber { get; private set; }
    public decimal SourceAmount { get; private set; }
    public string Currency { get; private set; } = null!;
    public int Version { get; private set; }
    public Guid? SupersedesAllocationSetId { get; private set; }
    public CashFlowAllocationSetStatus Status { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? FinalizedBy { get; private set; }
    public DateTimeOffset? FinalizedAt { get; private set; }
    public IReadOnlyList<CashFlowAllocation> Allocations => _allocations.AsReadOnly();
    public decimal AllocatedAmount => _allocations.Sum(item => item.Amount);
    public decimal RemainingAmount => SourceAmount - AllocatedAmount;

    public static CashFlowAllocationSet Create(
        Guid id,
        Guid financialBookId,
        Guid sourceJournalEntryId,
        int sourceLineNumber,
        decimal sourceAmount,
        string currency,
        Guid createdBy,
        DateTimeOffset createdAt,
        int version = 1,
        Guid? supersedesAllocationSetId = null)
    {
        if (financialBookId == Guid.Empty || sourceJournalEntryId == Guid.Empty || createdBy == Guid.Empty)
            throw new ArgumentException("Book, source entry, and actor identifiers are required.");
        if (sourceLineNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(sourceLineNumber));
        if (sourceAmount <= 0)
            throw new ArgumentOutOfRangeException(nameof(sourceAmount));
        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
            throw new ArgumentException("A three-letter currency is required.", nameof(currency));
        if (version < 1 || (version == 1 && supersedesAllocationSetId.HasValue) ||
            (version > 1 && !supersedesAllocationSetId.HasValue))
            throw new ArgumentException("Allocation version and superseded set are inconsistent.");

        return new CashFlowAllocationSet(
            id, financialBookId, sourceJournalEntryId, sourceLineNumber,
            sourceAmount, currency.Trim().ToUpperInvariant(), version,
            supersedesAllocationSetId, createdBy, createdAt);
    }

    public CashFlowAllocation AddAllocation(
        Guid id,
        decimal amount,
        CashFlowActivity activity,
        string category,
        int? counterpartLineNumber = null,
        string? memo = null)
    {
        EnsureDraft();
        if (id == Guid.Empty)
            throw new ArgumentException("Allocation id cannot be empty.", nameof(id));
        if (amount <= 0 || amount > 999999999999999.9999m ||
            decimal.Round(amount, 4) != amount)
            throw new DomainException("Allocation amount must be positive and fit the supported 19,4 decimal precision.");
        if (!Enum.IsDefined(activity))
            throw new ArgumentOutOfRangeException(nameof(activity));
        if (string.IsNullOrWhiteSpace(category))
            throw new DomainException("A stable cash-flow category code is required.");

        var categoryCode = category.Trim().ToUpperInvariant();
        if (categoryCode.Length > MaxCategoryLength ||
            categoryCode[0] is < 'A' or > 'Z' ||
            categoryCode.Any(character =>
                (character is < 'A' or > 'Z') &&
                (character is < '0' or > '9') &&
                character != '_'))
            throw new DomainException(
                $"Category must be a stable code containing only letters, digits, and underscores, up to {MaxCategoryLength} characters.");
        if (counterpartLineNumber is null or <= 0 || counterpartLineNumber == SourceLineNumber)
            throw new DomainException("Every cash-flow allocation must reference a different positive counterpart journal line.");
        if (memo?.Length > MaxMemoLength)
            throw new ArgumentException($"Memo cannot exceed {MaxMemoLength} characters.", nameof(memo));
        if (amount > RemainingAmount)
            throw new DomainException("Allocations cannot exceed the source cash line amount.");

        var allocation = new CashFlowAllocation(
            id, Id, amount, activity, categoryCode, counterpartLineNumber, memo?.Trim());
        _allocations.Add(allocation);
        return allocation;
    }

    public void RemoveAllocation(Guid allocationId)
    {
        EnsureDraft();
        var allocation = _allocations.SingleOrDefault(item => item.Id == allocationId);
        if (allocation is null)
            throw new DomainException("Cash-flow allocation was not found.");
        _allocations.Remove(allocation);
    }

    public void Finalize(Guid actorId, DateTimeOffset finalizedAt)
    {
        EnsureDraft();
        if (actorId == Guid.Empty)
            throw new ArgumentException("Finalizing actor is required.", nameof(actorId));
        if (_allocations.Count == 0 || AllocatedAmount != SourceAmount)
            throw new DomainException("A cash-flow allocation set can only be finalized when allocations exactly equal the source line amount.");

        Status = CashFlowAllocationSetStatus.Finalized;
        FinalizedBy = actorId;
        FinalizedAt = finalizedAt;
    }

    public void MarkSuperseded()
    {
        if (Status != CashFlowAllocationSetStatus.Finalized)
            throw new DomainException("Only a finalized allocation set can be superseded.");
        Status = CashFlowAllocationSetStatus.Superseded;
    }

    private void EnsureDraft()
    {
        if (Status != CashFlowAllocationSetStatus.Draft)
            throw new DomainException("Finalized cash-flow allocation sets are immutable. Create a new version to reclassify.");
    }
}

public sealed class CashFlowAllocation : Entity
{
    private CashFlowAllocation() : base(Guid.NewGuid()) { }

    internal CashFlowAllocation(
        Guid id,
        Guid allocationSetId,
        decimal amount,
        CashFlowActivity activity,
        string category,
        int? counterpartLineNumber,
        string? memo) : base(id)
    {
        AllocationSetId = allocationSetId;
        Amount = amount;
        Activity = activity;
        Category = category;
        CounterpartLineNumber = counterpartLineNumber;
        Memo = memo;
    }

    public Guid AllocationSetId { get; private set; }
    public decimal Amount { get; private set; }
    public CashFlowActivity Activity { get; private set; }
    public string Category { get; private set; } = null!;
    public int? CounterpartLineNumber { get; private set; }
    public string? Memo { get; private set; }
}
