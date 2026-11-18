using ArtaFin.Domain.Common;

namespace ArtaFin.Domain.Finance;

public enum LeasePaymentFrequency : byte
{
    Monthly = 1,
    Quarterly = 3,
    SemiAnnual = 6,
    Annual = 12
}

public enum LeasePaymentTiming : byte
{
    InAdvance = 1,
    InArrears = 2
}

public enum LeaseContractStatus : byte
{
    Active = 1,
    Completed = 2,
    Terminated = 3
}

/// <summary>
/// Contract-level source of truth for lease accounting terms. The effective rate
/// is expressed per configured payment period; no annual conversion or day-count
/// convention is inferred by this aggregate.
/// </summary>
public sealed class LeaseContract : AggregateRoot
{
    public const int MaxContractNumberLength = 100;
    public const int MaxDescriptionLength = 500;

    public Guid FinancialBookId { get; }
    public string ContractNumber { get; }
    public string Description { get; }
    public DateOnly CommencementDate { get; }
    public DateOnly FirstPaymentDate { get; }
    public DateOnly ContractEndDate { get; }
    public Money InitialLiability { get; }
    public decimal EffectivePeriodicRate { get; }
    public LeasePaymentFrequency PaymentFrequency { get; }
    public LeasePaymentTiming PaymentTiming { get; }
    public Guid LeaseLiabilityAccountId { get; }
    public Guid InterestExpenseAccountId { get; }
    public Guid PaymentAccountId { get; }
    public LeaseContractStatus Status { get; private set; }

    private LeaseContract(
        Guid id,
        Guid financialBookId,
        string contractNumber,
        string description,
        DateOnly commencementDate,
        DateOnly firstPaymentDate,
        DateOnly contractEndDate,
        Money initialLiability,
        decimal effectivePeriodicRate,
        LeasePaymentFrequency paymentFrequency,
        LeasePaymentTiming paymentTiming,
        Guid leaseLiabilityAccountId,
        Guid interestExpenseAccountId,
        Guid paymentAccountId) : base(id)
    {
        FinancialBookId = financialBookId;
        ContractNumber = contractNumber;
        Description = description;
        CommencementDate = commencementDate;
        FirstPaymentDate = firstPaymentDate;
        ContractEndDate = contractEndDate;
        InitialLiability = initialLiability;
        EffectivePeriodicRate = effectivePeriodicRate;
        PaymentFrequency = paymentFrequency;
        PaymentTiming = paymentTiming;
        LeaseLiabilityAccountId = leaseLiabilityAccountId;
        InterestExpenseAccountId = interestExpenseAccountId;
        PaymentAccountId = paymentAccountId;
        Status = LeaseContractStatus.Active;
    }

    public static LeaseContract Create(
        Guid id,
        Guid financialBookId,
        string contractNumber,
        string description,
        DateOnly commencementDate,
        DateOnly firstPaymentDate,
        DateOnly contractEndDate,
        Money initialLiability,
        decimal effectivePeriodicRate,
        LeasePaymentFrequency paymentFrequency,
        LeasePaymentTiming paymentTiming,
        Guid leaseLiabilityAccountId,
        Guid interestExpenseAccountId,
        Guid paymentAccountId)
    {
        if (financialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(financialBookId));
        if (string.IsNullOrWhiteSpace(contractNumber))
            throw new ArgumentException("Contract number is required.", nameof(contractNumber));

        var normalizedContractNumber = contractNumber.Trim();
        if (normalizedContractNumber.Length > MaxContractNumberLength)
            throw new ArgumentException($"Contract number cannot exceed {MaxContractNumberLength} characters.", nameof(contractNumber));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Contract description is required.", nameof(description));

        var normalizedDescription = description.Trim();
        if (normalizedDescription.Length > MaxDescriptionLength)
            throw new ArgumentException($"Contract description cannot exceed {MaxDescriptionLength} characters.", nameof(description));

        if (initialLiability.Amount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(initialLiability), "Initial lease liability must be positive.");

        if (effectivePeriodicRate < 0m)
            throw new ArgumentOutOfRangeException(nameof(effectivePeriodicRate), "Effective periodic rate cannot be negative.");

        if (!Enum.IsDefined(paymentFrequency))
            throw new ArgumentOutOfRangeException(nameof(paymentFrequency));
        if (!Enum.IsDefined(paymentTiming))
            throw new ArgumentOutOfRangeException(nameof(paymentTiming));

        if (firstPaymentDate < commencementDate ||
            (paymentTiming == LeasePaymentTiming.InArrears && firstPaymentDate == commencementDate))
            throw new ArgumentException("First payment date is inconsistent with the commencement date and payment timing.", nameof(firstPaymentDate));

        if (contractEndDate < firstPaymentDate)
            throw new ArgumentException("Contract end date cannot precede the first payment date.", nameof(contractEndDate));

        if (leaseLiabilityAccountId == Guid.Empty ||
            interestExpenseAccountId == Guid.Empty ||
            paymentAccountId == Guid.Empty)
            throw new ArgumentException("All lease accounting account ids are required.");

        if (leaseLiabilityAccountId == interestExpenseAccountId ||
            leaseLiabilityAccountId == paymentAccountId ||
            interestExpenseAccountId == paymentAccountId)
            throw new ArgumentException("Lease liability, interest expense, and payment accounts must be different.");

        return new LeaseContract(
            id,
            financialBookId,
            normalizedContractNumber,
            normalizedDescription,
            commencementDate,
            firstPaymentDate,
            contractEndDate,
            initialLiability,
            effectivePeriodicRate,
            paymentFrequency,
            paymentTiming,
            leaseLiabilityAccountId,
            interestExpenseAccountId,
            paymentAccountId);
    }

    public void Complete(DateOnly completedOn)
    {
        if (Status != LeaseContractStatus.Active)
            throw new DomainException("Only an active lease contract can be completed.");
        if (completedOn < CommencementDate || completedOn > ContractEndDate)
            throw new DomainException("Completion date must be within the lease contract term.");

        Status = LeaseContractStatus.Completed;
    }

    public void Terminate(DateOnly terminatedOn)
    {
        if (Status != LeaseContractStatus.Active)
            throw new DomainException("Only an active lease contract can be terminated.");
        if (terminatedOn < CommencementDate)
            throw new DomainException("Termination date cannot precede commencement.");

        Status = LeaseContractStatus.Terminated;
    }
}
