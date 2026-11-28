using ArtaFin.Application.Features.Accounts.GetAccountBalances;
using MediatR;

namespace ArtaFin.Api.Endpoints;

internal static class AccountBalancesEndpoints
{
    public static IEndpointRouteBuilder MapAccountBalancesEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/financial-books/{financialBookId:guid}/account-balances",
                GetAsync)
            .WithName("GetAccountBalances")
            .WithTags("Accounts")
            .RequireAuthorization("Permission:Accounts.Read")
            .Produces<AccountBalancesResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        Guid financialBookId,
        ISender sender,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 50)
    {
        var errors = new Dictionary<string, string[]>();
        if (page < 1)
            errors["page"] = ["Page must be greater than zero."];
        if (pageSize is < 1 or > 100)
            errors["pageSize"] = ["Page size must be between 1 and 100."];
        if (page > 0 && pageSize > 0 && page > int.MaxValue / pageSize)
            errors["page"] = ["Requested page is too large."];

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var result = await sender.Send(
            new GetAccountBalancesQuery(financialBookId, page, pageSize),
            cancellationToken);

        return Results.Ok(new AccountBalancesResponse(
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.Currency,
            result.Items.Select(item => new AccountBalanceItemResponse(
                item.AccountId,
                item.Code,
                item.Name,
                item.Type.ToString(),
                item.NormalBalance.ToString(),
                item.BalancePolicy.ToString(),
                item.Status.ToString(),
                item.TotalDebits,
                item.TotalCredits,
                item.Balance)).ToArray()));
    }
}

internal sealed record AccountBalancesResponse(
    int Page,
    int PageSize,
    int TotalCount,
    string Currency,
    IReadOnlyList<AccountBalanceItemResponse> Items);

internal sealed record AccountBalanceItemResponse(
    Guid AccountId,
    string Code,
    string Name,
    string Type,
    string NormalBalance,
    string BalancePolicy,
    string Status,
    decimal TotalDebits,
    decimal TotalCredits,
    decimal Balance);
