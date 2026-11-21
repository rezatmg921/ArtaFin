using System.Globalization;
using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Idempotency;
using ArtaFin.Application.Features.JournalEntries.CreateJournalEntry;
using ArtaFin.Domain.Finance;
using MediatR;

namespace ArtaFin.Application.Features.Income.CreateIncome;

/// <summary>
/// Records income as a debit to the receiving Asset account and a credit to an Income account.
/// The posted journal entry remains the accounting source of truth.
/// </summary>
public sealed class CreateIncomeCommandHandler(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    IAccountRepository accountRepository,
    IJournalEntryRepository journalEntryRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<CreateIncomeCommand, Guid>
{
    public async Task<Guid> Handle(CreateIncomeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.FinancialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(command));
        if (command.IncomeAccountId == Guid.Empty)
            throw new ArgumentException("Income account id cannot be empty.", nameof(command));
        if (command.DepositAccountId == Guid.Empty)
            throw new ArgumentException("Deposit account id cannot be empty.", nameof(command));
        if (command.IncomeAccountId == command.DepositAccountId)
            throw new IncomeAccountsMustBeDifferentException();
        if (command.Amount.Amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(command.Amount), "Income amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(command.Description))
            throw new ArgumentException("Description is required.", nameof(command));
        if (command.Description.Trim().Length > JournalEntry.MaxDescriptionLength)
            throw new ArgumentException($"Description cannot exceed {JournalEntry.MaxDescriptionLength} characters.", nameof(command));

        IdempotencyRequestHash.ValidateKey(command.IdempotencyKey);
        var idempotencyKey = command.IdempotencyKey?.Trim().ToUpperInvariant();


        var book = await financialBookRepository.GetByIdAsync(command.FinancialBookId, cancellationToken);
        if (book is null)
            throw new JournalEntryAccessDeniedException();

        var now = timeProvider.GetUtcNow();
        if (!book.IsOwnedBy(currentUser.UserId))
        {
            var member = await financialBookRepository.GetMemberAsync(book.Id, currentUser.UserId, cancellationToken);
            if (member is null || !member.CanPostJournalEntriesAt(now))
                throw new JournalEntryAccessDeniedException();
        }

        var requestHash = IdempotencyRequestHash.Compute(
            "income",
            command.FinancialBookId.ToString("N"),
            command.IncomeAccountId.ToString("N"),
            command.DepositAccountId.ToString("N"),
            command.Description.Trim(),
            command.Amount.Amount.ToString("G29", CultureInfo.InvariantCulture),
            command.Amount.Currency);

        var entryId = Guid.Empty;
        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (idempotencyKey is not null)
            {
                await unitOfWork.AcquireIdempotencyLockAsync(
                    book.Id, currentUser.UserId, idempotencyKey, ct);

                var existing = await journalEntryRepository.GetByIdempotencyKeyAsync(
                    book.Id, currentUser.UserId, idempotencyKey, ct);

                if (existing is not null)
                {
                    if (!StringComparer.Ordinal.Equals(existing.RequestHash, requestHash))
                        throw new IdempotencyKeyConflictException();

                    entryId = existing.Id;
                    return;
                }
            }

            if (!StringComparer.Ordinal.Equals(command.Amount.Currency, book.BaseCurrency.Code))
                throw new JournalEntryBaseCurrencyMismatchException();

            var incomeAccount = await accountRepository.GetByIdAsync(book.Id, command.IncomeAccountId, ct);
            var depositAccount = await accountRepository.GetByIdAsync(book.Id, command.DepositAccountId, ct);

            if (incomeAccount is null || depositAccount is null ||
                incomeAccount.Status != AccountStatus.Active || depositAccount.Status != AccountStatus.Active)
                throw new JournalEntryAccountUnavailableException();

            if (incomeAccount.Type != AccountType.Income)
                throw new IncomeAccountTypeInvalidException();
            if (depositAccount.Type != AccountType.Asset)
                throw new IncomeDepositAccountTypeInvalidException();

            var entry = JournalEntry.Post(
                Guid.NewGuid(),
                book.Id,
                now,
                command.Description,
                [
                    new JournalLine(depositAccount.Id, PostingSide.Debit, command.Amount),
                    new JournalLine(incomeAccount.Id, PostingSide.Credit, command.Amount)
                ],
                idempotencyUserId: idempotencyKey is null ? null : currentUser.UserId,
                idempotencyKey: idempotencyKey,
                requestHash: idempotencyKey is null ? null : requestHash);

            await unitOfWork.EnsureCanPostAsync(book.Id, entry.Lines, ct);
            await journalEntryRepository.AddAsync(entry, ct);
            await unitOfWork.SaveChangesAsync(ct);
            entryId = entry.Id;
        }, cancellationToken);

        return entryId;
    }
}
