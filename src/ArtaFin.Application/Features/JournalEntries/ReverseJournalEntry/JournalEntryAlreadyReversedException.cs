namespace ArtaFin.Application.Features.JournalEntries.ReverseJournalEntry;

/// <summary>
/// Indicates that an original journal entry already has a reversal entry.
/// </summary>
public sealed class JournalEntryAlreadyReversedException()
    : Exception("This journal entry has already been reversed.");
