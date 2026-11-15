namespace ArtaFin.Application.Features.JournalEntries.CreateJournalEntry;

/// <summary>Indicates that the financial book is missing or the caller cannot post to it.</summary>
public sealed class JournalEntryAccessDeniedException()
    : Exception("The financial book was not found or is not accessible to the current user.");

/// <summary>Indicates that an account is missing, belongs to another book, or is inactive.</summary>
public sealed class JournalEntryAccountUnavailableException()
    : Exception("One or more journal entry accounts are unavailable in this financial book.");

/// <summary>Indicates that a journal entry uses a currency other than the financial book's base currency.</summary>
public sealed class JournalEntryBaseCurrencyMismatchException()
    : Exception("Every journal line must use the financial book's base currency.");
