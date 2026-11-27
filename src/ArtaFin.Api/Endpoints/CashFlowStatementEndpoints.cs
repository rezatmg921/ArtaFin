using ArtaFin.Application.Features.Reports.GetCashFlowStatement;
using MediatR;

namespace ArtaFin.Api.Endpoints;

internal static class CashFlowStatementEndpoints
{
    public static IEndpointRouteBuilder MapCashFlowStatementEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/financial-books/{financialBookId:guid}/reports/cash-flow-statement",
                GetAsync)
            .WithName("GetCashFlowStatement")
            .WithTags("Reports")
            .RequireAuthorization("Permission:Reports.Read")
            .Produces<CashFlowStatementResponse>()
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
            new GetCashFlowStatementQuery(financialBookId, from, to),
            cancellationToken);

        return Results.Ok(new CashFlowStatementResponse(
            result.From,
            result.To,
            result.Currency,
            result.OpeningCashAndCashEquivalents,
            result.Activities.Select(item => new CashFlowActivityResponse(
                item.Activity.ToString(),
                item.CashReceipts,
                item.CashPayments,
                item.NetCashFlow)).ToArray(),
            result.Categories.Select(item => new CashFlowCategoryResponse(
                item.Activity.ToString(),
                item.Category,
                item.CashReceipts,
                item.CashPayments,
                item.NetCashFlow)).ToArray(),
            result.NetIncreaseDecrease,
            result.ClosingCashAndCashEquivalents,
            result.ReconciliationDifference,
            result.IsReconciled,
            result.InternalCashTransfers.Select(item => new CashFlowInternalTransferResponse(
                item.JournalEntryId,
                item.PostedAt,
                item.Description,
                item.FromAccountCode,
                item.FromAccountName,
                item.ToAccountCode,
                item.ToAccountName,
                item.Amount,
                item.Currency)).ToArray(),
            result.Exceptions.Select(item => new CashFlowStatementExceptionResponse(
                item.Code,
                item.Message,
                item.JournalEntryId,
                item.SourceLineNumber,
                item.PostedAt,
                item.Description,
                item.Amount,
                item.Currency)).ToArray(),
            result.NonCashDisclosures.Select(item => new CashFlowNonCashDisclosureResponse(
                item.Id,
                item.JournalEntryId,
                item.SourceLineNumber,
                item.PostedAt,
                item.Description,
                item.Amount,
                item.Currency,
                item.Activity.ToString(),
                item.Category,
                item.Memo)).ToArray()));
    }
}

internal sealed record CashFlowStatementResponse(
    DateTimeOffset From,
    DateTimeOffset To,
    string Currency,
    decimal OpeningCashAndCashEquivalents,
    IReadOnlyList<CashFlowActivityResponse> Activities,
    IReadOnlyList<CashFlowCategoryResponse> Categories,
    decimal NetIncreaseDecrease,
    decimal ClosingCashAndCashEquivalents,
    decimal ReconciliationDifference,
    bool IsReconciled,
    IReadOnlyList<CashFlowInternalTransferResponse> InternalCashTransfers,
    IReadOnlyList<CashFlowStatementExceptionResponse> Exceptions,
    IReadOnlyList<CashFlowNonCashDisclosureResponse> NonCashDisclosures);

internal sealed record CashFlowActivityResponse(
    string Activity,
    decimal CashReceipts,
    decimal CashPayments,
    decimal NetCashFlow);

internal sealed record CashFlowCategoryResponse(
    string Activity,
    string Category,
    decimal CashReceipts,
    decimal CashPayments,
    decimal NetCashFlow);

internal sealed record CashFlowInternalTransferResponse(
    Guid JournalEntryId,
    DateTimeOffset PostedAt,
    string Description,
    string FromAccountCode,
    string FromAccountName,
    string ToAccountCode,
    string ToAccountName,
    decimal Amount,
    string Currency);

internal sealed record CashFlowStatementExceptionResponse(
    string Code,
    string Message,
    Guid JournalEntryId,
    int SourceLineNumber,
    DateTimeOffset PostedAt,
    string Description,
    decimal Amount,
    string Currency);

internal sealed record CashFlowNonCashDisclosureResponse(
    Guid Id,
    Guid JournalEntryId,
    int SourceLineNumber,
    DateTimeOffset PostedAt,
    string Description,
    decimal Amount,
    string Currency,
    string Activity,
    string Category,
    string? Memo);
