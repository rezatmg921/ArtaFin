using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.JournalEntries.CreateJournalEntry;
using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Features.LeaseAccounting;

public sealed class LeaseContractService(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    IAccountRepository accountRepository,
    ILeaseContractRepository leaseContractRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<LeaseContractResult> CreateAsync(
        Guid financialBookId,
        CreateLeaseContractInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var book = await EnsureCanManageAsync(financialBookId, cancellationToken);

        if (!StringComparer.Ordinal.Equals(
                book.BaseCurrency.Code,
                input.Currency.Trim().ToUpperInvariant()))
            throw new LeaseContractAccountInvalidException(
                "Lease liability currency must match the financial book base currency.");

        var liabilityAccount = await accountRepository.GetByIdAsync(
            financialBookId, input.LeaseLiabilityAccountId, cancellationToken);
        var interestAccount = await accountRepository.GetByIdAsync(
            financialBookId, input.InterestExpenseAccountId, cancellationToken);
        var paymentAccount = await accountRepository.GetByIdAsync(
            financialBookId, input.PaymentAccountId, cancellationToken);

        if (liabilityAccount is null || liabilityAccount.Status != AccountStatus.Active ||
            liabilityAccount.Type != AccountType.Liability ||
            liabilityAccount.FinancingLiabilityType != FinancingLiabilityType.LeaseLiabilities)
            throw new LeaseContractAccountInvalidException(
                "The lease liability account must be an active LeaseLiabilities financing-liability account in this book.");

        if (interestAccount is null || interestAccount.Status != AccountStatus.Active ||
            interestAccount.Type != AccountType.Expense)
            throw new LeaseContractAccountInvalidException(
                "The interest expense account must be an active Expense account in this book.");

        if (paymentAccount is null || paymentAccount.Status != AccountStatus.Active ||
            paymentAccount.Type != AccountType.Asset ||
            paymentAccount.CashFlowAccountType is not CashFlowAccountType.Cash and not CashFlowAccountType.CashEquivalent)
            throw new LeaseContractAccountInvalidException(
                "The payment account must be an active Asset account classified as Cash or CashEquivalent.");

        var money = new Money(input.InitialLiabilityAmount, input.Currency);
        var contract = LeaseContract.Create(
            Guid.NewGuid(),
            financialBookId,
            input.ContractNumber,
            input.Description,
            input.CommencementDate,
            input.FirstPaymentDate,
            input.ContractEndDate,
            money,
            input.EffectivePeriodicRate,
            input.PaymentFrequency,
            input.PaymentTiming,
            input.LeaseLiabilityAccountId,
            input.InterestExpenseAccountId,
            input.PaymentAccountId);

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            if (await leaseContractRepository.ExistsByContractNumberAsync(
                    financialBookId, contract.ContractNumber, token))
                throw new LeaseContractNumberAlreadyExistsException();

            await leaseContractRepository.AddAsync(contract, token);
            await leaseContractRepository.SaveChangesAsync(token);
        }, cancellationToken);

        return ToResult(contract);
    }

    public async Task<LeaseContractResult?> GetByIdAsync(
        Guid financialBookId,
        Guid leaseContractId,
        CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(financialBookId, cancellationToken);
        var contract = await leaseContractRepository.GetByIdAsync(
            financialBookId, leaseContractId, cancellationToken);
        return contract is null ? null : ToResult(contract);
    }

    public async Task<IReadOnlyList<LeaseContractResult>> GetByFinancialBookAsync(
        Guid financialBookId,
        CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(financialBookId, cancellationToken);
        var contracts = await leaseContractRepository.GetByFinancialBookAsync(
            financialBookId, cancellationToken);
        return contracts.Select(ToResult).ToArray();
    }

    private async Task<FinancialBook> EnsureCanManageAsync(
        Guid financialBookId,
        CancellationToken cancellationToken)
    {
        if (financialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(financialBookId));

        var book = await financialBookRepository.GetByIdAsync(financialBookId, cancellationToken);
        if (book is null)
            throw new JournalEntryAccessDeniedException();

        if (book.IsOwnedBy(currentUser.UserId))
            return book;

        var member = await financialBookRepository.GetMemberAsync(
            financialBookId, currentUser.UserId, cancellationToken);

        if (member is null || !member.CanPostJournalEntriesAt(timeProvider.GetUtcNow()))
            throw new JournalEntryAccessDeniedException();

        return book;
    }

    private static LeaseContractResult ToResult(LeaseContract contract) =>
        new(
            contract.Id,
            contract.FinancialBookId,
            contract.ContractNumber,
            contract.Description,
            contract.CommencementDate,
            contract.FirstPaymentDate,
            contract.ContractEndDate,
            contract.InitialLiability.Amount,
            contract.InitialLiability.Currency,
            contract.EffectivePeriodicRate,
            contract.PaymentFrequency.ToString(),
            contract.PaymentTiming.ToString(),
            contract.LeaseLiabilityAccountId,
            contract.InterestExpenseAccountId,
            contract.PaymentAccountId,
            contract.Status.ToString());
}

public sealed record CreateLeaseContractInput(
    string ContractNumber,
    string Description,
    DateOnly CommencementDate,
    DateOnly FirstPaymentDate,
    DateOnly ContractEndDate,
    decimal InitialLiabilityAmount,
    string Currency,
    decimal EffectivePeriodicRate,
    LeasePaymentFrequency PaymentFrequency,
    LeasePaymentTiming PaymentTiming,
    Guid LeaseLiabilityAccountId,
    Guid InterestExpenseAccountId,
    Guid PaymentAccountId);

public sealed record LeaseContractResult(
    Guid Id,
    Guid FinancialBookId,
    string ContractNumber,
    string Description,
    DateOnly CommencementDate,
    DateOnly FirstPaymentDate,
    DateOnly ContractEndDate,
    decimal InitialLiabilityAmount,
    string Currency,
    decimal EffectivePeriodicRate,
    string PaymentFrequency,
    string PaymentTiming,
    Guid LeaseLiabilityAccountId,
    Guid InterestExpenseAccountId,
    Guid PaymentAccountId,
    string Status);

public sealed class LeaseContractNotFoundException : Exception { }
public sealed class LeaseContractNumberAlreadyExistsException : Exception { }
public sealed class LeaseContractAccountInvalidException(string message) : Exception(message);
