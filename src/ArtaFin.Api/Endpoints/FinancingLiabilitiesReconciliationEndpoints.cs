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
            .RequireAuthorization("Permission:Reports.Read")
            .Produces<FinancingLiabilitiesReconciliationResponse>()
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
        return Results.Ok(new FinancingLiabilitiesReconciliationResponse(
            result.From,
            result.To,
            result.Currency,
            result.Accounts.Select(item => new FinancingLiabilityMovementResponse(
                item.AccountId,
                item.AccountCode,
                item.AccountName,
                item.Classification.ToString(),
                item.OpeningBalance,
                item.CashChanges,
                item.NonCashChanges,
                item.UnclassifiedChanges,
                item.ClosingBalance,
                item.ReconciliationDifference)).ToArray(),
            result.TotalOpeningBalance,
            result.TotalCashChanges,
            result.TotalNonCashChanges,
            result.TotalUnclassifiedChanges,
            result.TotalClosingBalance,
            result.TotalReconciliationDifference,
            result.IsComplete,
            result.Exceptions));
    }
}

internal sealed record FinancingLiabilitiesReconciliationResponse(
    DateTimeOffset From,
    DateTimeOffset To,
    string Currency,
    IReadOnlyList<FinancingLiabilityMovementResponse> Accounts,
    decimal TotalOpeningBalance,
    decimal TotalCashChanges,
    decimal TotalNonCashChanges,
    decimal TotalUnclassifiedChanges,
    decimal TotalClosingBalance,
    decimal TotalReconciliationDifference,
    bool IsComplete,
    IReadOnlyList<FinancingLiabilityMovementExceptionResult> Exceptions);

internal sealed record FinancingLiabilityMovementResponse(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    string Classification,
    decimal OpeningBalance,
    decimal CashChanges,
    decimal NonCashChanges,
    decimal UnclassifiedChanges,
    decimal ClosingBalance,
    decimal ReconciliationDifference);
