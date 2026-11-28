using ArtaFin.Application.Features.Reports.GetCashFlowReconciliation;
using MediatR;

namespace ArtaFin.Api.Endpoints;

internal static class CashFlowReconciliationEndpoints
{
    public static IEndpointRouteBuilder MapCashFlowReconciliationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/financial-books/{financialBookId:guid}/reports/cash-flow-reconciliation",
                GetAsync)
            .WithName("GetCashFlowReconciliation")
            .WithTags("Reports")
            .RequireAuthorization("Permission:Reports.Read")
            .Produces<CashFlowReconciliationResponse>()
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
            new GetCashFlowReconciliationQuery(financialBookId, from, to),
            cancellationToken);

        return Results.Ok(new CashFlowReconciliationResponse(
            result.From, result.To, result.TrialBalanceAsOf, result.Currency,
            result.CashAccountDebits, result.CashAccountCredits, result.InternalTransfers,
            result.ExternalCashInflowsFromTrialBalance, result.ExternalCashOutflowsFromTrialBalance,
            result.CashFlowReceipts, result.CashFlowPayments, result.ReceiptsDifference,
            result.PaymentsDifference, result.IsReconciled,
            result.Accounts.Select(item => new CashFlowReconciliationAccountResponse(
                item.AccountId, item.AccountCode, item.AccountName, item.Debits, item.Credits)).ToArray()));
    }
}

internal sealed record CashFlowReconciliationResponse(
    DateTimeOffset From,
    DateTimeOffset To,
    DateTimeOffset TrialBalanceAsOf,
    string Currency,
    decimal CashAccountDebits,
    decimal CashAccountCredits,
    decimal InternalTransfers,
    decimal ExternalCashInflowsFromTrialBalance,
    decimal ExternalCashOutflowsFromTrialBalance,
    decimal CashFlowReceipts,
    decimal CashFlowPayments,
    decimal ReceiptsDifference,
    decimal PaymentsDifference,
    bool IsReconciled,
    IReadOnlyList<CashFlowReconciliationAccountResponse> Accounts);

internal sealed record CashFlowReconciliationAccountResponse(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    decimal Debits,
    decimal Credits);
