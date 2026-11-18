using ArtaFin.Application.Features.Reports.GetFinancingLiabilitiesReconciliation;
using MediatR;

namespace ArtaFin.Api.Endpoints;

internal static class FinancingLiabilitiesReconciliationEndpoints
{
    public static IEndpointRouteBuilder MapFinancingLiabilitiesReconciliationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/financial-books/{financialBookId:guid}/reports/financing-liabilities-reconciliation",
                GetAsync)
            .WithName("GetFinancingLiabilitiesReconciliation")
            .WithTags("Reports")
            .RequireAuthorization()
            .Produces<FinancingLiabilitiesReconciliationResult>()
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
            new GetFinancingLiabilitiesReconciliationQuery(financialBookId, from, to),
            cancellationToken);
        return Results.Ok(result);
    }
}
