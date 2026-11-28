using ArtaFin.Application.Features.Reports.GetIncomeStatement;
using MediatR;

namespace ArtaFin.Api.Endpoints;

internal static class IncomeStatementEndpoints
{
    public static IEndpointRouteBuilder MapIncomeStatementEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/financial-books/{financialBookId:guid}/reports/income-statement",
                GetAsync)
            .WithName("GetIncomeStatement")
            .WithTags("Reports")
            .RequireAuthorization("Permission:Reports.Read")
            .Produces<IncomeStatementResponse>()
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
            new GetIncomeStatementQuery(financialBookId, from, to),
            cancellationToken);

        return Results.Ok(new IncomeStatementResponse(
            result.From,
            result.To,
            result.Currency,
            result.IncomeAccounts.Select(ToResponse).ToArray(),
            result.ExpenseAccounts.Select(ToResponse).ToArray(),
            result.TotalIncome,
            result.TotalExpenses,
            result.NetProfit));
    }

    private static IncomeStatementAccountResponse ToResponse(
        IncomeStatementAccountResult account) =>
        new(account.AccountId, account.Code, account.Name,
            account.Type.ToString(), account.Amount);
}

internal sealed record IncomeStatementResponse(
    DateTimeOffset From,
    DateTimeOffset To,
    string Currency,
    IReadOnlyList<IncomeStatementAccountResponse> IncomeAccounts,
    IReadOnlyList<IncomeStatementAccountResponse> ExpenseAccounts,
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal NetProfit);

internal sealed record IncomeStatementAccountResponse(
    Guid AccountId,
    string Code,
    string Name,
    string Type,
    decimal Amount);
