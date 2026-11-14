using ArtaFin.Application.Abstractions.Persistence;
using MediatR;

namespace ArtaFin.Application.Features.Accounts.UpdateAccount;

/// <summary>
/// Requests a rename of an account. MatchAny represents the If-Match wildcard.
/// Otherwise, at least one supplied strong row-version tag must match.
/// </summary>
public sealed record UpdateAccountCommand(
    Guid FinancialBookId,
    Guid AccountId,
    string Name,
    IReadOnlyList<byte[]>? ExpectedRowVersions,
    bool MatchAny) : IRequest<AccountReadSnapshot?>;
