using ArtaFin.Application.Features.Reports.GetTrialBalance;
using MediatR;

namespace ArtaFin.Api.Endpoints;

internal static class TrialBalanceEndpoints
{
    public static IEndpointRouteBuilder MapTrialBalanceEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/financial-books/{financialBookId:guid}/reports/trial-balance",
                GetAsync)
            .WithName("GetTrialBalance")
            .WithTags("Reports")
            .RequireAuthorization("Permission:Reports.Read")
            .Produces<TrialBalanceResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        Guid financialBookId,
        ISender sender,
        CancellationToken cancellationToken,
        DateTimeOffset? asOf = null)
    {
        var result = await sender.Send(
            new GetTrialBalanceQuery(financialBookId, asOf),
            cancellationToken);

        return Results.Ok(new TrialBalanceResponse(
            result.AsOf,
            result.Currency,
            result.Items.Select(item => new TrialBalanceAccountResponse(
                item.AccountId,
                item.Code,
                item.Name,
                item.Type.ToString(),
                item.TotalDebits,
                item.TotalCredits,
                item.DebitBalance,
                item.CreditBalance)).ToArray(),
            result.TotalDebitBalance,
            result.TotalCreditBalance));
    }
}

internal sealed record TrialBalanceResponse(
    DateTimeOffset AsOf,
    string Currency,
    IReadOnlyList<TrialBalanceAccountResponse> Items,
    decimal TotalDebitBalance,
    decimal TotalCreditBalance);

internal sealed record TrialBalanceAccountResponse(
    Guid AccountId,
    string Code,
    string Name,
    string Type,
    decimal TotalDebits,
    decimal TotalCredits,
    decimal DebitBalance,
    decimal CreditBalance);
