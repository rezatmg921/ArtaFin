using ArtaFin.Application.Abstractions.Persistence;
using MediatR;

namespace ArtaFin.Application.Features.Accounts.GetAccount;

/// <summary>Requests one account within its financial-book boundary.</summary>
public sealed record GetAccountQuery(
    Guid FinancialBookId,
    Guid AccountId) : IRequest<AccountReadSnapshot?>;
