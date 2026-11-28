using ArtaFin.Domain.Common;

namespace ArtaFin.Domain.Finance;

/// <summary>
/// Append-only audit record for correcting the equity movement
/// classification of an immutable journal line.
/// </summary>
public sealed class EquityMovementCorrection : AggregateRoot
{
    public const int MaxReasonLength = 500;

    public Guid FinancialBookId { get; }
    public Guid JournalEntryId { get; }
    public int LineNumber { get; }
    public EquityMovementType OriginalMovementType { get; }
    public EquityMovementType CorrectedMovementType { get; }
    public string Reason { get; }
    public Guid CreatedBy { get; }
    public DateTimeOffset CreatedAt { get; }

    private EquityMovementCorrection(
        Guid id,
        Guid financialBookId,
        Guid journalEntryId,
        int lineNumber,
        EquityMovementType originalMovementType,
        EquityMovementType correctedMovementType,
        string reason,
        Guid createdBy,
        DateTimeOffset createdAt) : base(id)
    {
        FinancialBookId = financialBookId;
        JournalEntryId = journalEntryId;
        LineNumber = lineNumber;
        OriginalMovementType = originalMovementType;
        CorrectedMovementType = correctedMovementType;
        Reason = reason;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public static EquityMovementCorrection Create(
        Guid id,
        Guid financialBookId,
        Guid journalEntryId,
        int lineNumber,
        EquityMovementType originalMovementType,
        EquityMovementType correctedMovementType,
        string reason,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty ||
            financialBookId == Guid.Empty ||
            journalEntryId == Guid.Empty ||
            createdBy == Guid.Empty)
        {
            throw new ArgumentException("Correction identifiers cannot be empty.");
        }

        if (lineNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(lineNumber));

        if (!Enum.IsDefined(originalMovementType) ||
            !Enum.IsDefined(correctedMovementType))
        {
            throw new DomainException("Equity movement type is invalid.");
        }

        if (originalMovementType == correctedMovementType)
        {
            throw new DomainException(
                "Corrected movement type must differ from the original type.");
        }

        if (correctedMovementType == EquityMovementType.Unclassified)
        {
            throw new DomainException(
                "A correction must specify a classified equity movement type.");
        }

        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Correction reason is required.");

        var normalizedReason = reason.Trim();

        if (normalizedReason.Length > MaxReasonLength)
        {
            throw new DomainException(
                $"Correction reason cannot exceed {MaxReasonLength} characters.");
        }

        return new EquityMovementCorrection(
            id,
            financialBookId,
            journalEntryId,
            lineNumber,
            originalMovementType,
            correctedMovementType,
            normalizedReason,
            createdBy,
            createdAt);
    }
}
