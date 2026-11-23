using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Idempotency;
using ArtaFin.Domain.Common;
using ArtaFin.Domain.Finance;
using ArtaFin.Domain.Obligations;

namespace ArtaFin.Application.Features.ObligationPayments;

public sealed record ObligationPaymentAllocationRequest(Guid InstallmentId, decimal Amount);

public sealed record ObligationPaymentSnapshot(
    Guid Id, Guid FinancialBookId, Guid FinancialObligationId, Guid CashAccountId,
    Guid JournalEntryId, decimal Amount, string Currency, DateOnly PaymentDate,
    string Description, string? Reference, Guid CreatedBy, DateTimeOffset CreatedAt,
    IReadOnlyList<ObligationPaymentAllocationSnapshot> Allocations);

public sealed record ObligationPaymentAllocationSnapshot(Guid InstallmentId, decimal Amount);

public sealed record CreateObligationPaymentResult(Guid PaymentId, Guid JournalEntryId, bool WasAlreadyCreated);

/// <summary>Posts receipts and disbursements against obligation installments.</summary>
public sealed class FinancialObligationPaymentService(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    IAccountRepository accountRepository,
    IFinancialObligationPaymentRepository paymentRepository,
    IJournalEntryRepository journalEntryRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<CreateObligationPaymentResult> CreateAsync(
        Guid financialBookId, Guid obligationId, Guid cashAccountId,
        decimal amount, string currency, DateOnly paymentDate, string description,
        string? reference, string idempotencyKey,
        IReadOnlyList<ObligationPaymentAllocationRequest> allocations,
        CancellationToken cancellationToken)
    {
        IdempotencyRequestHash.ValidateKey(idempotencyKey);
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency-Key is required.", nameof(idempotencyKey));
        ArgumentNullException.ThrowIfNull(allocations);

        var book = await EnsureBookAccessAsync(financialBookId, write: true, cancellationToken);
        var userId = currentUser.UserId;
        var money = new Money(amount, currency);
        if (!StringComparer.Ordinal.Equals(money.Currency, book.BaseCurrency.Code))
            throw new DomainException("Obligation payments must use the financial book's base currency.");

        var normalizedAllocations = allocations
            .OrderBy(x => x.InstallmentId)
            .Select(x => new FinancialObligationPaymentAllocationTerm(x.InstallmentId, x.Amount))
            .ToArray();
        var hash = IdempotencyRequestHash.Compute(
            obligationId.ToString("N"), cashAccountId.ToString("N"),
            money.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture), money.Currency,
            paymentDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            description?.Trim() ?? string.Empty, reference?.Trim() ?? string.Empty,
            string.Join("|", normalizedAllocations.Select(x =>
                $"{x.InstallmentId:N}:{x.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}")));

        CreateObligationPaymentResult? result = null;
        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await unitOfWork.AcquireIdempotencyLockAsync(financialBookId, userId, idempotencyKey, ct);
            var existing = await paymentRepository.GetByIdempotencyKeyAsync(
                financialBookId, userId, idempotencyKey, ct);
            if (existing is not null)
            {
                if (!StringComparer.Ordinal.Equals(existing.RequestHash, hash))
                    throw new IdempotencyKeyConflictException();
                result = new CreateObligationPaymentResult(existing.Id, existing.JournalEntryId, true);
                return;
            }

            var cashAccount = await accountRepository.GetByIdAsync(financialBookId, cashAccountId, ct);
            if (cashAccount is null || cashAccount.Status != AccountStatus.Active)
                throw new KeyNotFoundException("An active cash or bank account in this financial book was not found.");
            if (cashAccount.Type != AccountType.Asset)
                throw new DomainException("The payment account must be an asset account.");

            var obligation = await paymentRepository.GetObligationForPaymentAsync(
                financialBookId, obligationId, ct);
            if (obligation is null)
                throw new KeyNotFoundException("Financial obligation was not found.");
            if (obligation.Status == FinancialObligationStatus.Cancelled)
                throw new DomainException("A cancelled obligation cannot receive payments.");
            if (!StringComparer.Ordinal.Equals(obligation.Currency, money.Currency))
                throw new DomainException("Payment currency must match the obligation currency.");

            var installmentIds = obligation.Installments.Select(x => x.Id).ToHashSet();
            if (normalizedAllocations.Any(x => !installmentIds.Contains(x.InstallmentId)))
                throw new DomainException("Every payment allocation must reference an installment of this obligation.");

            var alreadyPaid = await paymentRepository.GetAllocatedAmountForObligationAsync(obligation.Id, ct);
            if (money.Amount > obligation.OriginalAmount - alreadyPaid)
                throw new DomainException("Payment amount exceeds the outstanding obligation balance.");

            var paidByInstallment = await paymentRepository.GetAllocatedAmountsByInstallmentAsync(obligation.Id, ct);
            foreach (var allocation in normalizedAllocations)
            {
                var installment = obligation.Installments.Single(x => x.Id == allocation.InstallmentId);
                var alreadyAllocated = paidByInstallment.GetValueOrDefault(installment.Id);
                if (allocation.Amount <= 0 || allocation.Amount > installment.Amount - alreadyAllocated)
                    throw new DomainException("An allocation exceeds the outstanding balance of its installment.");
            }

            var now = timeProvider.GetUtcNow();
            var journalLines = obligation.Direction switch
            {
                FinancialObligationDirection.Receivable => new[]
                {
                    new JournalLine(cashAccountId, PostingSide.Debit, money),
                    new JournalLine(obligation.ControlAccountId, PostingSide.Credit, money)
                },
                FinancialObligationDirection.Payable => new[]
                {
                    new JournalLine(obligation.ControlAccountId, PostingSide.Debit, money),
                    new JournalLine(cashAccountId, PostingSide.Credit, money)
                },
                _ => throw new ArgumentOutOfRangeException()
            };
            var journalEntry = JournalEntry.Post(
                Guid.NewGuid(), financialBookId, now,
                description.Trim(), journalLines);

            var payment = FinancialObligationPayment.Create(
                Guid.NewGuid(), financialBookId, obligationId, cashAccountId,
                journalEntry.Id, money.Amount, money.Currency, paymentDate,
                description, reference, userId, now, idempotencyKey, hash, normalizedAllocations);

            var cumulativePaid = alreadyPaid + money.Amount;
            obligation.ApplyPayment(cumulativePaid, now);

            await unitOfWork.EnsureCanPostAsync(financialBookId, journalEntry.Lines, ct);
            await journalEntryRepository.AddAsync(journalEntry, ct);
            await paymentRepository.AddAsync(payment, ct);
            await unitOfWork.SaveChangesAsync(ct);

            result = new CreateObligationPaymentResult(payment.Id, journalEntry.Id, false);
        }, cancellationToken);

        return result ?? throw new InvalidOperationException("Payment transaction completed without a result.");
    }

    public async Task<ObligationPaymentSnapshot?> GetByIdAsync(
        Guid financialBookId, Guid paymentId, CancellationToken cancellationToken)
    {
        await EnsureBookAccessAsync(financialBookId, write: false, cancellationToken);
        var payment = await paymentRepository.GetByIdAsync(financialBookId, paymentId, cancellationToken);
        return payment is null ? null : ToSnapshot(payment);
    }

    public async Task<IReadOnlyList<ObligationPaymentSnapshot>> ListByObligationAsync(
        Guid financialBookId, Guid obligationId, int skip, int take, CancellationToken cancellationToken)
    {
        await EnsureBookAccessAsync(financialBookId, write: false, cancellationToken);
        if (skip < 0 || take is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(take), "Skip must be non-negative and take must be between 1 and 100.");
        var items = await paymentRepository.ListByObligationAsync(
            financialBookId, obligationId, skip, take, cancellationToken);
        return items.Select(ToSnapshot).ToArray();
    }

    private async Task<FinancialBook> EnsureBookAccessAsync(
        Guid financialBookId, bool write, CancellationToken cancellationToken)
    {
        if (currentUser.UserId == Guid.Empty)
            throw new UnauthorizedAccessException("An authenticated user is required.");
        var book = await financialBookRepository.GetByIdAsync(financialBookId, cancellationToken);
        if (book is null)
            throw new KeyNotFoundException("Financial book was not found.");
        if (book.IsOwnedBy(currentUser.UserId))
            return book;

        var member = await financialBookRepository.GetMemberAsync(
            financialBookId, currentUser.UserId, cancellationToken);
        if (member is null || !member.IsActiveAt(timeProvider.GetUtcNow()))
            throw new UnauthorizedAccessException("The user does not have access to this financial book.");
        if (write && member.Role != FinancialBookRole.Accountant)
            throw new UnauthorizedAccessException("Accountant membership is required to post obligation payments.");
        return book;
    }

    private static ObligationPaymentSnapshot ToSnapshot(FinancialObligationPayment x) => new(
        x.Id, x.FinancialBookId, x.FinancialObligationId, x.CashAccountId,
        x.JournalEntryId, x.Amount, x.Currency, x.PaymentDate, x.Description,
        x.Reference, x.CreatedBy, x.CreatedAt,
        x.Allocations.Select(a => new ObligationPaymentAllocationSnapshot(a.InstallmentId, a.Amount)).ToArray());
}
