using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Domain.Common;
using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Features.CashFlowAllocations;

public sealed class CashFlowAllocationService(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    ICashFlowAllocationRepository allocationRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<CashFlowAllocationSetResult> CreateDraftAsync(
        Guid financialBookId,
        Guid journalEntryId,
        int lineNumber,
        CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(financialBookId, cancellationToken);
        CashFlowAllocationSet? created = null;

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await allocationRepository.LockSourceEntryAsync(
                financialBookId, journalEntryId, token);

            var source = await allocationRepository.GetSourceLineAsync(
                financialBookId, journalEntryId, lineNumber, token);

            if (source is null)
                throw new CashFlowSourceLineNotFoundException();

            if (source.AccountType != AccountType.Asset ||
                source.CashFlowAccountType is not CashFlowAccountType.Cash and not CashFlowAccountType.CashEquivalent)
                throw new CashFlowSourceLineInvalidException(
                    "The source journal line must belong to an account classified as Cash or CashEquivalent.");

            if (await allocationRepository.GetDraftForSourceAsync(
                    journalEntryId, lineNumber, token) is not null)
                throw new CashFlowAllocationDraftAlreadyExistsException();

            var version = await allocationRepository.GetNextVersionAsync(
                journalEntryId, lineNumber, token);

            if (version != 1)
                throw new CashFlowAllocationRevisionConflictException();

            created = CashFlowAllocationSet.Create(
                Guid.NewGuid(),
                financialBookId,
                journalEntryId,
                lineNumber,
                source.Amount,
                source.Currency,
                currentUser.UserId,
                timeProvider.GetUtcNow());

            await allocationRepository.AddAsync(created, token);
            await allocationRepository.SaveChangesAsync(token);
        }, cancellationToken);

        return ToResult(created!);
    }

    public async Task<CashFlowAllocationSetResult> GetAsync(
        Guid financialBookId,
        Guid allocationSetId,
        CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(financialBookId, cancellationToken);
        var set = await allocationRepository.GetByIdAsync(
            financialBookId, allocationSetId, tracking: false, cancellationToken);
        return set is null ? throw new CashFlowAllocationSetNotFoundException() : ToResult(set);
    }

    public async Task<CashFlowAllocationSetResult> AddAllocationAsync(
        Guid financialBookId,
        Guid allocationSetId,
        AddCashFlowAllocationInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        await EnsureCanManageAsync(financialBookId, cancellationToken);
        CashFlowAllocationSet? updated = null;

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await allocationRepository.LockAllocationSetAsync(financialBookId, allocationSetId, token);
            var set = await allocationRepository.GetByIdAsync(
                financialBookId, allocationSetId, tracking: true, token)
                ?? throw new CashFlowAllocationSetNotFoundException();

            if (set.Status != CashFlowAllocationSetStatus.Draft)
                throw new DomainException(
                    "Finalized cash-flow allocation sets are immutable. Create a new version to reclassify.");

            if (!input.CounterpartLineNumber.HasValue)
                throw new CashFlowSourceLineInvalidException(
                    "A counterpart journal line is required to classify the cash movement.");

            var source = await allocationRepository.GetSourceLineAsync(
                financialBookId, set.SourceJournalEntryId, set.SourceLineNumber, token);
            var counterpart = await allocationRepository.GetSourceLineAsync(
                financialBookId, set.SourceJournalEntryId, input.CounterpartLineNumber.Value, token);

            if (source is null || counterpart is null ||
                source.Side == counterpart.Side)
                throw new CashFlowSourceLineInvalidException(
                    "The counterpart must be a different, existing journal line on the opposite posting side.");

            if (counterpart.CashFlowAccountType is CashFlowAccountType.Cash or CashFlowAccountType.CashEquivalent)
                throw new CashFlowSourceLineInvalidException(
                    "Transfers between cash and cash equivalents are internal movements and must not be allocated as cash flows.");

            var alreadyAllocatedToCounterpart = set.Allocations
                .Where(item => item.CounterpartLineNumber == input.CounterpartLineNumber)
                .Sum(item => item.Amount);
            if (alreadyAllocatedToCounterpart + input.Amount > counterpart.Amount)
                throw new DomainException(
                    "Allocations referencing a counterpart line cannot exceed that journal line amount.");

            set.AddAllocation(Guid.NewGuid(), input.Amount, input.Activity,
                input.Category, input.CounterpartLineNumber, input.Memo);
            await allocationRepository.SaveChangesAsync(token);
            updated = set;
        }, cancellationToken);

        return ToResult(updated!);
    }

    public async Task<CashFlowAllocationSetResult> RemoveAllocationAsync(
        Guid financialBookId,
        Guid allocationSetId,
        Guid allocationId,
        CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(financialBookId, cancellationToken);
        CashFlowAllocationSet? updated = null;

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await allocationRepository.LockAllocationSetAsync(financialBookId, allocationSetId, token);
            var set = await allocationRepository.GetByIdAsync(
                financialBookId, allocationSetId, tracking: true, token)
                ?? throw new CashFlowAllocationSetNotFoundException();

            set.RemoveAllocation(allocationId);
            await allocationRepository.SaveChangesAsync(token);
            updated = set;
        }, cancellationToken);

        return ToResult(updated!);
    }

    public async Task CancelDraftAsync(
        Guid financialBookId,
        Guid allocationSetId,
        CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(financialBookId, cancellationToken);

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var sourceSnapshot = await allocationRepository.GetByIdAsync(
                financialBookId, allocationSetId, tracking: false, token)
                ?? throw new CashFlowAllocationSetNotFoundException();

            await allocationRepository.LockSourceEntryAsync(
                financialBookId, sourceSnapshot.SourceJournalEntryId, token);
            await allocationRepository.LockAllocationSetAsync(
                financialBookId, allocationSetId, token);

            var set = await allocationRepository.GetByIdAsync(
                financialBookId, allocationSetId, tracking: true, token)
                ?? throw new CashFlowAllocationSetNotFoundException();

            if (set.Status != CashFlowAllocationSetStatus.Draft)
                throw new DomainException("Only a Draft cash-flow allocation set can be cancelled.");

            allocationRepository.DeleteDraft(set);
            await allocationRepository.SaveChangesAsync(token);
        }, cancellationToken);
    }

    public async Task<CashFlowAllocationSetResult> FinalizeAsync(
        Guid financialBookId,
        Guid allocationSetId,
        CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(financialBookId, cancellationToken);
        CashFlowAllocationSet? finalized = null;

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var sourceSnapshot = await allocationRepository.GetByIdAsync(
                financialBookId, allocationSetId, tracking: false, token)
                ?? throw new CashFlowAllocationSetNotFoundException();

            // Always acquire locks in source-entry then allocation-set order.
            // This keeps revision and finalization paths from taking them in reverse order.
            await allocationRepository.LockSourceEntryAsync(
                financialBookId, sourceSnapshot.SourceJournalEntryId, token);
            await allocationRepository.LockAllocationSetAsync(
                financialBookId, allocationSetId, token);

            var set = await allocationRepository.GetByIdAsync(
                financialBookId, allocationSetId, tracking: true, token)
                ?? throw new CashFlowAllocationSetNotFoundException();

            if (set.SupersedesAllocationSetId.HasValue)
            {
                var previous = await allocationRepository.GetByIdAsync(
                    financialBookId, set.SupersedesAllocationSetId.Value, tracking: true, token)
                    ?? throw new CashFlowAllocationSetNotFoundException();

                if (previous.Status != CashFlowAllocationSetStatus.Finalized)
                    throw new CashFlowAllocationRevisionConflictException();

                previous.MarkSuperseded();
            }
            else if (await allocationRepository.GetFinalizedForSourceAsync(
                         set.SourceJournalEntryId, set.SourceLineNumber, token) is not null)
            {
                throw new CashFlowAllocationRevisionConflictException();
            }

            set.Finalize(currentUser.UserId, timeProvider.GetUtcNow());
            await allocationRepository.SaveChangesAsync(token);
            finalized = set;
        }, cancellationToken);

        return ToResult(finalized!);
    }

    public async Task<CashFlowAllocationSetResult> CreateRevisionAsync(
        Guid financialBookId,
        Guid allocationSetId,
        CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(financialBookId, cancellationToken);
        CashFlowAllocationSet? created = null;

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var original = await allocationRepository.GetByIdAsync(
                financialBookId, allocationSetId, tracking: false, token)
                ?? throw new CashFlowAllocationSetNotFoundException();

            await allocationRepository.LockSourceEntryAsync(
                financialBookId, original.SourceJournalEntryId, token);

            original = await allocationRepository.GetByIdAsync(
                financialBookId, allocationSetId, tracking: false, token)
                ?? throw new CashFlowAllocationSetNotFoundException();

            if (original.Status != CashFlowAllocationSetStatus.Finalized)
                throw new CashFlowAllocationRevisionConflictException();

            if (await allocationRepository.GetDraftForSourceAsync(
                    original.SourceJournalEntryId, original.SourceLineNumber, token) is not null)
                throw new CashFlowAllocationDraftAlreadyExistsException();

            var version = await allocationRepository.GetNextVersionAsync(
                original.SourceJournalEntryId, original.SourceLineNumber, token);

            created = CashFlowAllocationSet.Create(
                Guid.NewGuid(),
                financialBookId,
                original.SourceJournalEntryId,
                original.SourceLineNumber,
                original.SourceAmount,
                original.Currency,
                currentUser.UserId,
                timeProvider.GetUtcNow(),
                version,
                original.Id);

            await allocationRepository.AddAsync(created, token);
            await allocationRepository.SaveChangesAsync(token);
        }, cancellationToken);

        return ToResult(created!);
    }

    private async Task EnsureCanManageAsync(
        Guid financialBookId,
        CancellationToken cancellationToken)
    {
        if (financialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(financialBookId));

        var book = await financialBookRepository.GetByIdAsync(financialBookId, cancellationToken);
        if (book is null)
            throw new FinancialBookAccessDeniedException();

        if (book.IsOwnedBy(currentUser.UserId))
            return;

        var member = await financialBookRepository.GetMemberAsync(
            financialBookId, currentUser.UserId, cancellationToken);

        if (member is null || !member.CanPostJournalEntriesAt(timeProvider.GetUtcNow()))
            throw new FinancialBookAccessDeniedException();
    }

    private static CashFlowAllocationSetResult ToResult(CashFlowAllocationSet set) =>
        new(
            set.Id,
            set.FinancialBookId,
            set.SourceJournalEntryId,
            set.SourceLineNumber,
            set.SourceAmount,
            set.Currency,
            set.Version,
            set.SupersedesAllocationSetId,
            set.Status.ToString(),
            set.CreatedBy,
            set.CreatedAt,
            set.FinalizedBy,
            set.FinalizedAt,
            set.AllocatedAmount,
            set.RemainingAmount,
            set.Allocations.Select(item => new CashFlowAllocationResult(
                item.Id,
                item.Amount,
                item.Activity.ToString(),
                item.Category,
                item.CounterpartLineNumber,
                item.Memo)).ToArray());
}

public sealed record AddCashFlowAllocationInput(
    decimal Amount,
    CashFlowActivity Activity,
    string Category,
    int? CounterpartLineNumber,
    string? Memo);

public sealed record CashFlowAllocationSetResult(
    Guid Id,
    Guid FinancialBookId,
    Guid SourceJournalEntryId,
    int SourceLineNumber,
    decimal SourceAmount,
    string Currency,
    int Version,
    Guid? SupersedesAllocationSetId,
    string Status,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    Guid? FinalizedBy,
    DateTimeOffset? FinalizedAt,
    decimal AllocatedAmount,
    decimal RemainingAmount,
    IReadOnlyList<CashFlowAllocationResult> Allocations);

public sealed record CashFlowAllocationResult(
    Guid Id,
    decimal Amount,
    string Activity,
    string Category,
    int? CounterpartLineNumber,
    string? Memo);

public sealed class CashFlowSourceLineNotFoundException : Exception { }
public sealed class CashFlowSourceLineInvalidException(string message) : Exception(message);
public sealed class CashFlowAllocationSetNotFoundException : Exception { }
public sealed class CashFlowAllocationDraftAlreadyExistsException : Exception { }
public sealed class CashFlowAllocationRevisionConflictException : Exception { }
