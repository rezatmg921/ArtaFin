namespace ArtaFin.Application.Features.Accounts.GetAccountLedger;

public sealed class AccountLedgerNotFoundException()
    : Exception("The account was not found in this financial book.");
