namespace ArtaFin.Application.Features.Accounts.UpdateAccount;

/// <summary>
/// Indicates that the account no longer matches the strong entity tag supplied by the client.
/// </summary>
public sealed class AccountPreconditionFailedException()
    : Exception("The account has changed since it was last retrieved. Reload it before updating.");
