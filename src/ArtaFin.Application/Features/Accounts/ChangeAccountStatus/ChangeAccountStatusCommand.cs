using ArtaFin.Application.Abstractions.Persistence;
using MediatR;

namespace ArtaFin.Application.Features.Accounts.ChangeAccountStatus;

/// <summary>
/// Requests an account activation-state transition using an HTTP If-Match condition.
/// </summary>
public sealed record ChangeAccountStatusCommand(
    Guid FinancialBookId,
    Guid AccountId,
    IReadOnlyList<byte[]>? ExpectedRowVersions,
    bool MatchAny,
    bool Reactivate) : IRequest<AccountReadSnapshot?>;
