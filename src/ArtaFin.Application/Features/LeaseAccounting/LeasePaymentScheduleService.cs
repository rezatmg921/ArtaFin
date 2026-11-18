using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.JournalEntries.CreateJournalEntry;
using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Features.LeaseAccounting;

public sealed class LeasePaymentScheduleService(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    ILeaseContractRepository leaseContractRepository,
    ILeasePaymentScheduleRepository scheduleRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<LeasePaymentScheduleResult> CreateAsync(
        Guid financialBookId,
        Guid leaseContractId,
        IReadOnlyList<LeaseScheduledPayment> payments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payments);
        await EnsureCanManageAsync(financialBookId, cancellationToken);

        LeasePaymentSchedule? created = null;
        LeaseContract? contract = null;

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            contract = await leaseContractRepository.GetByIdAsync(
                financialBookId, leaseContractId, token);
            if (contract is null)
                throw new LeaseContractNotFoundException();
            if (contract.Status != LeaseContractStatus.Active)
                throw new LeasePaymentScheduleStateConflictException(
                    "A payment schedule can only be created for an active lease contract.");

            // The serializable transaction protects the contract/version range.
            // Only one draft may exist; a new version supersedes the latest
            // finalized version after the new draft is finalized.
            var latest = await scheduleRepository.GetLatestAsync(
                financialBookId, leaseContractId, token);

            if (latest?.Status == LeasePaymentScheduleStatus.Draft)
                throw new LeasePaymentScheduleDraftAlreadyExistsException();
            if (latest?.Status == LeasePaymentScheduleStatus.Superseded)
                throw new LeasePaymentScheduleStateConflictException(
                    "The latest schedule version is superseded; reload the contract schedule history.");

            var version = (latest?.Version ?? 0) + 1;
            created = LeasePaymentSchedule.Create(
                Guid.NewGuid(),
                contract,
                version,
                latest?.Id,
                currentUser.UserId,
                timeProvider.GetUtcNow(),
                payments);

            await scheduleRepository.AddAsync(created, token);
            await scheduleRepository.SaveChangesAsync(token);
        }, cancellationToken);

        return ToResult(created!, contract!);
    }

    public async Task<IReadOnlyList<LeasePaymentScheduleResult>> GetByContractAsync(
        Guid financialBookId,
        Guid leaseContractId,
        CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(financialBookId, cancellationToken);
        var contract = await leaseContractRepository.GetByIdAsync(
            financialBookId, leaseContractId, cancellationToken);
        if (contract is null)
            throw new LeaseContractNotFoundException();

        var schedules = await scheduleRepository.GetByContractAsync(
            financialBookId, leaseContractId, cancellationToken);
        return schedules.Select(schedule => ToResult(schedule, contract)).ToArray();
    }

    public async Task<LeasePaymentScheduleResult> FinalizeAsync(
        Guid financialBookId,
        Guid leaseContractId,
        Guid scheduleId,
        CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(financialBookId, cancellationToken);

        LeasePaymentSchedule? finalized = null;
        LeaseContract? contract = null;

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            contract = await leaseContractRepository.GetByIdAsync(
                financialBookId, leaseContractId, token);
            if (contract is null)
                throw new LeaseContractNotFoundException();
            if (contract.Status != LeaseContractStatus.Active)
                throw new LeasePaymentScheduleStateConflictException(
                    "A payment schedule can only be finalized for an active lease contract.");

            finalized = await scheduleRepository.GetByIdAsync(
                financialBookId, leaseContractId, scheduleId, token);
            if (finalized is null)
                throw new LeasePaymentScheduleNotFoundException();
            if (finalized.Status != LeasePaymentScheduleStatus.Draft)
                throw new LeasePaymentScheduleStateConflictException(
                    "Only the current draft payment schedule can be finalized.");

            var previousFinalized = await scheduleRepository.GetLatestFinalizedAsync(
                financialBookId, leaseContractId, token);

            if (previousFinalized is not null && previousFinalized.Id != finalized.Id)
                previousFinalized.MarkSuperseded();

            finalized.Finalize(currentUser.UserId, timeProvider.GetUtcNow());
            await scheduleRepository.SaveChangesAsync(token);
        }, cancellationToken);

        return ToResult(finalized!, contract!);
    }

    private async Task EnsureCanManageAsync(
        Guid financialBookId,
        CancellationToken cancellationToken)
    {
        if (financialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(financialBookId));

        var book = await financialBookRepository.GetByIdAsync(financialBookId, cancellationToken);
        if (book is null)
            throw new JournalEntryAccessDeniedException();

        if (book.IsOwnedBy(currentUser.UserId))
            return;

        var member = await financialBookRepository.GetMemberAsync(
            financialBookId, currentUser.UserId, cancellationToken);

        if (member is null || !member.CanPostJournalEntriesAt(timeProvider.GetUtcNow()))
            throw new JournalEntryAccessDeniedException();
    }

    private static LeasePaymentScheduleResult ToResult(
        LeasePaymentSchedule schedule,
        LeaseContract contract)
    {
        var contractualPayments = schedule.Payments
            .OrderBy(payment => payment.PeriodNumber)
            .Select(payment => new LeaseScheduledPayment(
                payment.PaymentDate,
                payment.PaymentAmount,
                payment.EffectivePeriodicRate))
            .ToArray();

        var calculations = LeasePaymentScheduleCalculator.Calculate(
            contract.InitialLiability,
            contract.FirstPaymentDate,
            contract.ContractEndDate,
            contractualPayments);

        var paymentResults = schedule.Payments
            .OrderBy(payment => payment.PeriodNumber)
            .Zip(calculations, (payment, calculation) =>
                new LeasePaymentSchedulePaymentResult(
                    payment.PeriodNumber,
                    payment.PaymentDate,
                    payment.PaymentAmount,
                    payment.EffectivePeriodicRate,
                    calculation.Calculation.OpeningLiability.Amount,
                    calculation.Calculation.InterestExpense.Amount,
                    calculation.Calculation.PrincipalReduction.Amount,
                    calculation.Calculation.ClosingLiability.Amount,
                    contract.InitialLiability.Currency))
            .ToArray();

        return new LeasePaymentScheduleResult(
            schedule.Id,
            schedule.FinancialBookId,
            schedule.LeaseContractId,
            schedule.Version,
            schedule.SupersedesScheduleId,
            schedule.Status.ToString(),
            schedule.CreatedBy,
            schedule.CreatedAt,
            schedule.FinalizedBy,
            schedule.FinalizedAt,
            paymentResults);
    }
}

public sealed record LeasePaymentSchedulePaymentResult(
    int PeriodNumber,
    DateOnly PaymentDate,
    decimal PaymentAmount,
    decimal EffectivePeriodicRate,
    decimal OpeningLiability,
    decimal InterestExpense,
    decimal PrincipalReduction,
    decimal ClosingLiability,
    string Currency);

public sealed record LeasePaymentScheduleResult(
    Guid Id,
    Guid FinancialBookId,
    Guid LeaseContractId,
    int Version,
    Guid? SupersedesScheduleId,
    string Status,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    Guid? FinalizedBy,
    DateTimeOffset? FinalizedAt,
    IReadOnlyList<LeasePaymentSchedulePaymentResult> Payments);

public sealed class LeasePaymentScheduleNotFoundException : Exception { }
public sealed class LeasePaymentScheduleDraftAlreadyExistsException : Exception { }
public sealed class LeasePaymentScheduleStateConflictException(string message) : Exception(message);
