namespace ArtaFin.Application.Features.Accounts.DeleteAccount;

/// <summary>
/// The account is part of accounting history and therefore cannot be physically deleted.
/// </summary>
public sealed class AccountHasJournalEntriesException()
    : Exception("The account is referenced by journal entries. Deactivate it instead of deleting it.");