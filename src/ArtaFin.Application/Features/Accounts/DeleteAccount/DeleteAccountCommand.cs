using MediatR;

namespace ArtaFin.Application.Features.Accounts.DeleteAccount;

/// <summary>
/// Deletes an unused account when the supplied If-Match condition is satisfied.
/// </summary>
public sealed record DeleteAccountCommand(
    Guid FinancialBookId,
    Guid AccountId,
    IReadOnlyList<byte[]>? ExpectedRowVersions,
    bool MatchAny) : IRequest<bool>;