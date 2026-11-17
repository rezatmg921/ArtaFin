using ArtaFin.Application.Features.Reports.GetBalanceSheet;
using MediatR;

namespace ArtaFin.Api.Endpoints;

internal static class BalanceSheetEndpoints
{
    public static IEndpointRouteBuilder MapBalanceSheetEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/financial-books/{financialBookId:guid}/reports/balance-sheet",
                GetAsync)
            .WithName("GetBalanceSheet")
            .WithTags("Reports")
            .RequireAuthorization()
            .Produces<BalanceSheetResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        Guid financialBookId,
        ISender sender,
        CancellationToken cancellationToken,
        DateTimeOffset? asOf = null)
    {
        var result = await sender.Send(
            new GetBalanceSheetQuery(financialBookId, asOf),
            cancellationToken);

        return Results.Ok(new BalanceSheetResponse(
            result.AsOf,
            result.Currency,
            result.Assets.Select(ToResponse).ToArray(),
            result.Liabilities.Select(ToResponse).ToArray(),
            result.Equity.Select(ToResponse).ToArray(),
            result.UnclosedNetIncome,
            result.TotalAssets,
            result.TotalLiabilities,
            result.TotalEquity,
            result.TotalLiabilitiesAndEquity,
            result.Difference));
    }

    private static BalanceSheetAccountResponse ToResponse(
        BalanceSheetAccountResult account) =>
        new(account.AccountId, account.Code, account.Name,
            account.Type.ToString(), account.Amount);
}

internal sealed record BalanceSheetResponse(
    DateTimeOffset AsOf,
    string Currency,
    IReadOnlyList<BalanceSheetAccountResponse> Assets,
    IReadOnlyList<BalanceSheetAccountResponse> Liabilities,
    IReadOnlyList<BalanceSheetAccountResponse> Equity,
    decimal UnclosedNetIncome,
    decimal TotalAssets,
    decimal TotalLiabilities,
    decimal TotalEquity,
    decimal TotalLiabilitiesAndEquity,
    decimal Difference);

internal sealed record BalanceSheetAccountResponse(
    Guid AccountId,
    string Code,
    string Name,
    string Type,
    decimal Amount);
