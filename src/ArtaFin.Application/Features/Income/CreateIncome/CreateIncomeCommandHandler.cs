using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
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

        if (!StringComparer.Ordinal.Equals(command.Amount.Currency, book.BaseCurrency.Code))
            throw new JournalEntryBaseCurrencyMismatchException();

        var incomeAccount = await accountRepository.GetByIdAsync(book.Id, command.IncomeAccountId, cancellationToken);
        var depositAccount = await accountRepository.GetByIdAsync(book.Id, command.DepositAccountId, cancellationToken);

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
            ]);

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await unitOfWork.EnsureCanPostAsync(book.Id, entry.Lines, ct);
            await journalEntryRepository.AddAsync(entry, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);
        return entry.Id;
    }
}
