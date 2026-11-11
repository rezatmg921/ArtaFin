using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Domain.Finance;
using MediatR;

namespace ArtaFin.Application.Features.Accounts.ListAccounts;

public sealed record ListAccountsQuery(
    Guid FinancialBookId,
    AccountStatus? Status,
    string? SearchTerm,
    int Page,
    int PageSize) : IRequest<AccountPage>;
