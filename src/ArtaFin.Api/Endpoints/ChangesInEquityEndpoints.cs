using ArtaFin.Application.Features.Reports.GetChangesInEquity;
using MediatR;

namespace ArtaFin.Api.Endpoints;

internal static class ChangesInEquityEndpoints
{
    public static IEndpointRouteBuilder MapChangesInEquityEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/financial-books/{financialBookId:guid}/reports/changes-in-equity",
                GetAsync)
            .WithName("GetChangesInEquity")
            .WithTags("Reports")
            .RequireAuthorization("Permission:Reports.Read")
            .Produces<ChangesInEquityResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        Guid financialBookId,
        DateTimeOffset from,
        DateTimeOffset to,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (from >= to)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["to"] = ["To must be later than From. The period includes From and excludes To."]
            });
        }

        var result = await sender.Send(
            new GetChangesInEquityQuery(financialBookId, from, to),
            cancellationToken);

        return Results.Ok(new ChangesInEquityResponse(
            result.From,
            result.To,
            result.Currency,
            result.Components,
            result.OpeningEquity,
            result.TotalIncreases,
            result.TotalDecreases,
            result.ClosingEquity,
            result.OpeningUnclosedNetIncome,
            result.ClosingUnclosedNetIncome,
            result.OpeningTotalEquity,
            result.ClosingTotalEquity));
    }
}

internal sealed record ChangesInEquityResponse(
    DateTimeOffset From,
    DateTimeOffset To,
    string Currency,
    IReadOnlyList<ChangesInEquityAccountResult> Components,
    decimal OpeningEquity,
    decimal TotalIncreases,
    decimal TotalDecreases,
    decimal ClosingEquity,
    decimal OpeningUnclosedNetIncome,
    decimal ClosingUnclosedNetIncome,
    decimal OpeningTotalEquity,
    decimal ClosingTotalEquity);
