using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Domain.Finance;
using MediatR;

namespace ArtaFin.Application.Features.Accounts.UpdateAccount;

/// <summary>Renames an account and optionally changes its operational balance policy.</summary>
public sealed record UpdateAccountCommand(
    Guid FinancialBookId,
    Guid AccountId,
    string Name,
    IReadOnlyList<byte[]>? ExpectedRowVersions,
    bool MatchAny,
    AccountBalancePolicy? BalancePolicy = null) : IRequest<AccountReadSnapshot?>;
