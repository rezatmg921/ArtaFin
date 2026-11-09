using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Features.JournalEntries.CreateJournalEntry;

/// <summary>
/// Coordinates access checks and account validation before asking the Domain
/// to create an immutable, balanced posted journal entry.
/// </summary>
public sealed class CreateJournalEntryCommandHandler(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    IAccountRepository accountRepository,
    IJournalEntryRepository journalEntryRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Guid> HandleAsync(
        CreateJournalEntryCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.FinancialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(command));

        var financialBook = await financialBookRepository.GetByIdAsync(
            command.FinancialBookId,
            cancellationToken);

        if (financialBook is null)
            throw new JournalEntryAccessDeniedException();

        var now = timeProvider.GetUtcNow();
        var userId = currentUser.UserId;

        if (!financialBook.IsOwnedBy(userId))
        {
            var membership = await financialBookRepository.GetMemberAsync(
                financialBook.Id,
                userId,
                cancellationToken);

            if (membership is null || !membership.CanPostJournalEntriesAt(now))
                throw new JournalEntryAccessDeniedException();
        }

        ArgumentNullException.ThrowIfNull(command.Lines);

        var lines = new List<JournalLine>(command.Lines.Count);
        foreach (var requestedLine in command.Lines)
        {
            ArgumentNullException.ThrowIfNull(requestedLine);

            var account = await accountRepository.GetByIdAsync(
                financialBook.Id,
                requestedLine.AccountId,
                cancellationToken);

            if (account is null || account.Status != AccountStatus.Active)
                throw new JournalEntryAccountUnavailableException();

            lines.Add(new JournalLine(
                account.Id,
                requestedLine.Side,
                requestedLine.Amount));
        }

        var entry = JournalEntry.Post(
            Guid.NewGuid(),
            financialBook.Id,
            now,
            command.Description,
            lines);

        await journalEntryRepository.AddAsync(entry, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return entry.Id;
    }
}
