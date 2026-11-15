using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.JournalEntries.CreateJournalEntry;
using ArtaFin.Domain.Finance;
using MediatR;

namespace ArtaFin.Application.Features.Expenses.CreateExpense;

/// <summary>
/// Records an expense as a debit to an Expense account and a credit to an Asset account.
/// The posted journal entry remains the accounting source of truth.
/// </summary>
public sealed class CreateExpenseCommandHandler(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    IAccountRepository accountRepository,
    IJournalEntryRepository journalEntryRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<CreateExpenseCommand, Guid>
{
    public async Task<Guid> Handle(
        CreateExpenseCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.FinancialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(command));
        if (command.ExpenseAccountId == Guid.Empty)
            throw new ArgumentException("Expense account id cannot be empty.", nameof(command));
        if (command.PaymentAccountId == Guid.Empty)
            throw new ArgumentException("Payment account id cannot be empty.", nameof(command));
        if (command.ExpenseAccountId == command.PaymentAccountId)
            throw new ExpenseAccountsMustBeDifferentException();
        if (command.Amount.Amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(command.Amount), "Expense amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(command.Description))
            throw new ArgumentException("Description is required.", nameof(command));
        if (command.Description.Trim().Length > JournalEntry.MaxDescriptionLength)
            throw new ArgumentException(
                $"Description cannot exceed {JournalEntry.MaxDescriptionLength} characters.",
                nameof(command));

        var financialBook = await financialBookRepository.GetByIdAsync(
            command.FinancialBookId,
            cancellationToken);

        if (financialBook is null)
            throw new JournalEntryAccessDeniedException();

        var now = timeProvider.GetUtcNow();
        if (!financialBook.IsOwnedBy(currentUser.UserId))
        {
            var membership = await financialBookRepository.GetMemberAsync(
                financialBook.Id,
                currentUser.UserId,
                cancellationToken);

            if (membership is null || !membership.CanPostJournalEntriesAt(now))
                throw new JournalEntryAccessDeniedException();
        }

        if (!StringComparer.Ordinal.Equals(
                command.Amount.Currency,
                financialBook.BaseCurrency.Code))
        {
            throw new JournalEntryBaseCurrencyMismatchException();
        }

        var expenseAccount = await accountRepository.GetByIdAsync(
            financialBook.Id,
            command.ExpenseAccountId,
            cancellationToken);
        var paymentAccount = await accountRepository.GetByIdAsync(
            financialBook.Id,
            command.PaymentAccountId,
            cancellationToken);

        if (expenseAccount is null || paymentAccount is null ||
            expenseAccount.Status != AccountStatus.Active ||
            paymentAccount.Status != AccountStatus.Active)
        {
            throw new JournalEntryAccountUnavailableException();
        }

        if (expenseAccount.Type != AccountType.Expense)
            throw new ExpenseAccountTypeInvalidException();
        if (paymentAccount.Type != AccountType.Asset)
            throw new ExpensePaymentAccountTypeInvalidException();

        var entry = JournalEntry.Post(
            Guid.NewGuid(),
            financialBook.Id,
            now,
            command.Description,
            [
                new JournalLine(expenseAccount.Id, PostingSide.Debit, command.Amount),
                new JournalLine(paymentAccount.Id, PostingSide.Credit, command.Amount)
            ]);

        await journalEntryRepository.AddAsync(entry, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return entry.Id;
    }
}
