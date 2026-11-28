namespace ArtaFin.Application.Features.JournalEntries.ReverseJournalEntry;

public sealed class JournalEntryLinkedToPaymentException()
    : Exception("A journal entry linked to an obligation payment must be reversed through the payment reversal workflow.");
