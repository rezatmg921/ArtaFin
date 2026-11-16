using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Features.Accounts.CreateAccount;

public sealed record CreateAccountCommand(
    Guid FinancialBookId,
    string Code,
    string Name,
    AccountType Type,
    AccountBalancePolicy? BalancePolicy = null);
