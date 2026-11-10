using ArtaFin.Application.Abstractions.Persistence;
using MediatR;

namespace ArtaFin.Application.Features.Accounts.UpdateAccount;

/// <summary>
/// Requests a rename of an account. A null expected version represents If-Match: *.
/// </summary>
public sealed record UpdateAccountCommand(
    Guid FinancialBookId,
    Guid AccountId,
    string Name,
    byte[]? ExpectedRowVersion) : IRequest<AccountReadSnapshot?>;
