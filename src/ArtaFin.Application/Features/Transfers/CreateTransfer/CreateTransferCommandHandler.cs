using System.Globalization;
using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Idempotency;
using ArtaFin.Application.Features.JournalEntries.CreateJournalEntry;
using ArtaFin.Domain.Finance;
using MediatR;

namespace ArtaFin.Application.Features.Transfers.CreateTransfer;

/// <summary>
/// Posts a transfer as a debit to the destination asset account and a credit
/// to the source asset account. No separate transfer balance is maintained.
/// </summary>
public sealed class CreateTransferCommandHandler(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    IAccountRepository accountRepository,
    IJournalEntryRepository journalEntryRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<CreateTransferCommand, Guid>
{
    public async Task<Guid> Handle(CreateTransferCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.FinancialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(command));
        if (command.SourceAccountId == Guid.Empty)
            throw new ArgumentException("Source account id cannot be empty.", nameof(command));
        if (command.DestinationAccountId == Guid.Empty)
            throw new ArgumentException("Destination account id cannot be empty.", nameof(command));
        if (command.SourceAccountId == command.DestinationAccountId)
            throw new TransferAccountsMustBeDifferentException();
        if (command.Amount.Amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(command.Amount), "Transfer amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(command.Description))
            throw new ArgumentException("Description is required.", nameof(command));
        if (command.Description.Trim().Length > JournalEntry.MaxDescriptionLength)
            throw new ArgumentException(
                $"Description cannot exceed {JournalEntry.MaxDescriptionLength} characters.",
                nameof(command));

        IdempotencyRequestHash.ValidateKey(command.IdempotencyKey);

        var book = await financialBookRepository.GetByIdAsync(command.FinancialBookId, cancellationToken);
        if (book is null)
            throw new JournalEntryAccessDeniedException();

        var now = timeProvider.GetUtcNow();
        if (!book.IsOwnedBy(currentUser.UserId))
        {
            var member = await financialBookRepository.GetMemberAsync(
                book.Id, currentUser.UserId, cancellationToken);
            if (member is null || !member.CanPostJournalEntriesAt(now))
                throw new JournalEntryAccessDeniedException();
        }

        var requestHash = IdempotencyRequestHash.Compute(
            "transfer",
            command.FinancialBookId.ToString("N"),
            command.SourceAccountId.ToString("N"),
            command.DestinationAccountId.ToString("N"),
            command.Description.Trim(),
            command.Amount.Amount.ToString("G29", CultureInfo.InvariantCulture),
            command.Amount.Currency);

        var entryId = Guid.Empty;
        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (command.IdempotencyKey is not null)
            {
                await unitOfWork.AcquireIdempotencyLockAsync(
                    book.Id, currentUser.UserId, command.IdempotencyKey, ct);

                var existing = await journalEntryRepository.GetByIdempotencyKeyAsync(
                    book.Id, currentUser.UserId, command.IdempotencyKey, ct);

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

            var source = await accountRepository.GetByIdAsync(book.Id, command.SourceAccountId, ct);
            var destination = await accountRepository.GetByIdAsync(book.Id, command.DestinationAccountId, ct);

            if (source is null || destination is null ||
                source.Status != AccountStatus.Active || destination.Status != AccountStatus.Active)
                throw new JournalEntryAccountUnavailableException();

            if (source.Type != AccountType.Asset || destination.Type != AccountType.Asset)
                throw new TransferAccountTypeInvalidException();

            var entry = JournalEntry.Post(
                Guid.NewGuid(),
                book.Id,
                now,
                command.Description,
                [
                    new JournalLine(destination.Id, PostingSide.Debit, command.Amount),
                    new JournalLine(source.Id, PostingSide.Credit, command.Amount)
                ],
                idempotencyUserId: command.IdempotencyKey is null ? null : currentUser.UserId,
                idempotencyKey: command.IdempotencyKey,
                requestHash: command.IdempotencyKey is null ? null : requestHash);

            await unitOfWork.EnsureCanPostAsync(book.Id, entry.Lines, ct);
            await journalEntryRepository.AddAsync(entry, ct);
            await unitOfWork.SaveChangesAsync(ct);
            entryId = entry.Id;
        }, cancellationToken);

        return entryId;
    }
}
