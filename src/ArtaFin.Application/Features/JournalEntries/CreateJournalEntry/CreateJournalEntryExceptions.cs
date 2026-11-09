namespace ArtaFin.Application.Features.JournalEntries.CreateJournalEntry;

/// <summary>Indicates that the financial book is missing or the caller cannot post to it.</summary>
public sealed class JournalEntryAccessDeniedException()
    : Exception("The financial book was not found or is not accessible to the current user.");

/// <summary>Indicates that an account is missing, belongs to another book, or is inactive.</summary>
public sealed class JournalEntryAccountUnavailableException()
    : Exception("One or more journal entry accounts are unavailable in this financial book.");
