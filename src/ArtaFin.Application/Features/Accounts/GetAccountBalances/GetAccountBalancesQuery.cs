using MediatR;

namespace ArtaFin.Application.Features.Accounts.GetAccountBalances;

public sealed record GetAccountBalancesQuery(
    Guid FinancialBookId,
    int Page,
    int PageSize) : IRequest<AccountBalancesResult>;
