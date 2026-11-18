using ArtaFin.Domain.Common;

namespace ArtaFin.Domain.Finance;

public enum LeasePaymentScheduleStatus : byte
{
    Draft = 1,
    Finalized = 2,
    Superseded = 3
}

/// <summary>
/// Immutable dated cash-flow terms for one version of a lease contract.
/// A replacement is always a new version; an issued schedule is never edited.
/// </summary>
public sealed class LeasePaymentSchedule : AggregateRoot
{
    public const int MaxPayments = 1200;

    private readonly List<LeasePaymentScheduleLine> _payments = [];

    private LeasePaymentSchedule(
        Guid id,
        Guid financialBookId,
        Guid leaseContractId,
        int version,
        Guid? supersedesScheduleId,
        Guid createdBy,
        DateTimeOffset createdAt) : base(id)
    {
        FinancialBookId = financialBookId;
        LeaseContractId = leaseContractId;
        Version = version;
        SupersedesScheduleId = supersedesScheduleId;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        Status = LeasePaymentScheduleStatus.Draft;
    }

    private LeasePaymentSchedule() : base(Guid.NewGuid()) { }

    public Guid FinancialBookId { get; private set; }
    public Guid LeaseContractId { get; private set; }
    public int Version { get; private set; }
    public Guid? SupersedesScheduleId { get; private set; }
    public LeasePaymentScheduleStatus Status { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? FinalizedBy { get; private set; }
    public DateTimeOffset? FinalizedAt { get; private set; }
    public IReadOnlyList<LeasePaymentScheduleLine> Payments => _payments.AsReadOnly();

    public static LeasePaymentSchedule Create(
        Guid id,
        LeaseContract contract,
        int version,
        Guid? supersedesScheduleId,
        Guid createdBy,
        DateTimeOffset createdAt,
        IReadOnlyList<LeaseScheduledPayment> payments)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(payments);

        if (id == Guid.Empty || createdBy == Guid.Empty)
            throw new ArgumentException("Schedule and actor identifiers are required.");
        if (version < 1 || (version == 1 && supersedesScheduleId.HasValue) ||
            (version > 1 && !supersedesScheduleId.HasValue))
            throw new ArgumentException("Schedule version and superseded schedule are inconsistent.");
        if (supersedesScheduleId == id)
            throw new ArgumentException("A schedule cannot supersede itself.");
        if (payments.Count is < 1 or > MaxPayments)
            throw new ArgumentOutOfRangeException(nameof(payments),
                $"A schedule must contain between 1 and {MaxPayments} payments.");

        // Validate all dates, rates, amounts, currency and liability roll-forward
        // before creating any persisted rows.
        _ = LeasePaymentScheduleCalculator.Calculate(
            contract.InitialLiability,
            contract.FirstPaymentDate,
            contract.ContractEndDate,
            payments);

        var schedule = new LeasePaymentSchedule(
            id, contract.FinancialBookId, contract.Id, version,
            supersedesScheduleId, createdBy, createdAt);

        for (var index = 0; index < payments.Count; index++)
        {
            var payment = payments[index];
            schedule._payments.Add(new LeasePaymentScheduleLine(
                Guid.NewGuid(), schedule.Id, index + 1,
                payment.PaymentDate, payment.PaymentAmount,
                payment.EffectivePeriodicRate));
        }

        return schedule;
    }

    public void Finalize(Guid actorId, DateTimeOffset finalizedAt)
    {
        if (Status != LeasePaymentScheduleStatus.Draft)
            throw new DomainException("Only a draft lease payment schedule can be finalized.");
        if (actorId == Guid.Empty)
            throw new ArgumentException("Finalizing actor is required.", nameof(actorId));

        Status = LeasePaymentScheduleStatus.Finalized;
        FinalizedBy = actorId;
        FinalizedAt = finalizedAt;
    }

    public void MarkSuperseded()
    {
        if (Status != LeasePaymentScheduleStatus.Finalized)
            throw new DomainException("Only a finalized lease payment schedule can be superseded.");
        Status = LeasePaymentScheduleStatus.Superseded;
    }
}

/// <summary>A contractual payment row belonging to a schedule version.</summary>
public sealed class LeasePaymentScheduleLine : Entity
{
    private LeasePaymentScheduleLine() : base(Guid.NewGuid()) { }

    internal LeasePaymentScheduleLine(
        Guid id,
        Guid scheduleId,
        int periodNumber,
        DateOnly paymentDate,
        decimal paymentAmount,
        decimal effectivePeriodicRate) : base(id)
    {
        ScheduleId = scheduleId;
        PeriodNumber = periodNumber;
        PaymentDate = paymentDate;
        PaymentAmount = paymentAmount;
        EffectivePeriodicRate = effectivePeriodicRate;
    }

    public Guid ScheduleId { get; private set; }
    public int PeriodNumber { get; private set; }
    public DateOnly PaymentDate { get; private set; }
    public decimal PaymentAmount { get; private set; }
    public decimal EffectivePeriodicRate { get; private set; }
}
