using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Features.Accounts.CreateAccount;

/// <summary>
/// Requests creation of an account in the specified FinancialBook.
/// The authenticated user identity and server-controlled fields are intentionally omitted.
/// </summary>
public sealed record CreateAccountCommand(
    Guid FinancialBookId,
    string Code,
    string Name,
    AccountType Type);
