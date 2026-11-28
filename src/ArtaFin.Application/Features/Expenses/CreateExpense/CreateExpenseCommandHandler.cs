using System.Globalization;
using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Idempotency;
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

        IdempotencyRequestHash.ValidateKey(command.IdempotencyKey);
        var idempotencyKey = command.IdempotencyKey?.Trim().ToUpperInvariant();


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

        var requestHash = IdempotencyRequestHash.Compute(
            "expense",
            command.FinancialBookId.ToString("N"),
            command.ExpenseAccountId.ToString("N"),
            command.PaymentAccountId.ToString("N"),
            command.Description.Trim(),
            command.Amount.Amount.ToString("G29", CultureInfo.InvariantCulture),
            command.Amount.Currency);

        var entryId = Guid.Empty;
        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (idempotencyKey is not null)
            {
                await unitOfWork.AcquireIdempotencyLockAsync(
                    financialBook.Id, currentUser.UserId, idempotencyKey, ct);

                var existing = await journalEntryRepository.GetByIdempotencyKeyAsync(
                    financialBook.Id, currentUser.UserId, idempotencyKey, ct);

                if (existing is not null)
                {
                    if (!StringComparer.Ordinal.Equals(existing.RequestHash, requestHash))
                        throw new IdempotencyKeyConflictException();

                    entryId = existing.Id;
                    return;
                }
            }

            if (!StringComparer.Ordinal.Equals(
                    command.Amount.Currency,
                    financialBook.BaseCurrency.Code))
            {
                throw new JournalEntryBaseCurrencyMismatchException();
            }

            var expenseAccount = await accountRepository.GetByIdAsync(
                financialBook.Id, command.ExpenseAccountId, ct);
            var paymentAccount = await accountRepository.GetByIdAsync(
                financialBook.Id, command.PaymentAccountId, ct);

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
                ],
                idempotencyUserId: idempotencyKey is null ? null : currentUser.UserId,
                idempotencyKey: idempotencyKey,
                requestHash: idempotencyKey is null ? null : requestHash);

            await unitOfWork.EnsureCanPostAsync(financialBook.Id, entry.Lines, ct);
            await journalEntryRepository.AddAsync(entry, ct);
            await unitOfWork.SaveChangesAsync(ct);
            entryId = entry.Id;
        }, cancellationToken);

        return entryId;
    }
}
