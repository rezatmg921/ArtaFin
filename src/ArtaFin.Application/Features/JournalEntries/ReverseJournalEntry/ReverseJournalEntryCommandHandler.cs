using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.JournalEntries.CreateJournalEntry;
using ArtaFin.Domain.Finance;
using MediatR;

namespace ArtaFin.Application.Features.JournalEntries.ReverseJournalEntry;

/// <summary>
/// Coordinates authorization and persistence for a journal entry reversal.
/// The accounting rules themselves remain inside the JournalEntry aggregate.
/// </summary>
public sealed class ReverseJournalEntryCommandHandler(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    IJournalEntryRepository journalEntryRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<ReverseJournalEntryCommand, Guid>
{
    public async Task<Guid> Handle(
        ReverseJournalEntryCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.FinancialBookId == Guid.Empty)
            throw new ArgumentException(
                "Financial book id cannot be empty.",
                nameof(command));

        if (command.JournalEntryId == Guid.Empty)
            throw new ArgumentException(
                "Journal entry id cannot be empty.",
                nameof(command));

        if (string.IsNullOrWhiteSpace(command.Description))
            throw new ArgumentException(
                "Description is required.",
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

        var original = await journalEntryRepository.GetByIdAsync(
            financialBook.Id,
            command.JournalEntryId,
            cancellationToken);

        if (original is null)
            throw new JournalEntryAccessDeniedException();

        if (await journalEntryRepository.HasReversalAsync(
                financialBook.Id,
                original.Id,
                cancellationToken))
        {
            throw new JournalEntryAlreadyReversedException();
        }

        var reversal = JournalEntry.Reverse(
            Guid.NewGuid(),
            original,
            now,
            command.Description);

        await journalEntryRepository.AddAsync(reversal, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return reversal.Id;
    }
}
