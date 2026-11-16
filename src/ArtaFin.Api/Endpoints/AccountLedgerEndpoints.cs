using ArtaFin.Application.Features.Accounts.GetAccountLedger;
using MediatR;

namespace ArtaFin.Api.Endpoints;

internal static class AccountLedgerEndpoints
{
    public static IEndpointRouteBuilder MapAccountLedgerEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/financial-books/{financialBookId:guid}/accounts/{accountId:guid}/ledger",
                GetAsync)
            .WithName("GetAccountLedger")
            .WithTags("Accounts")
            .RequireAuthorization()
            .Produces<AccountLedgerResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        Guid financialBookId,
        Guid accountId,
        ISender sender,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 50,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null)
    {
        var errors = new Dictionary<string, string[]>();
        if (page < 1)
            errors["page"] = ["Page must be greater than zero."];
        if (pageSize is < 1 or > 100)
            errors["pageSize"] = ["Page size must be between 1 and 100."];
        if (page > 0 && pageSize > 0 && page > int.MaxValue / pageSize)
            errors["page"] = ["Requested page is too large."];
        if (from.HasValue && to.HasValue && from > to)
            errors["to"] = ["To date must be greater than or equal to From date."];

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var result = await sender.Send(
            new GetAccountLedgerQuery(
                financialBookId, accountId, from, to, page, pageSize),
            cancellationToken);

        return Results.Ok(new AccountLedgerResponse(
            result.AccountId,
            result.AccountCode,
            result.AccountName,
            result.AccountType.ToString(),
            result.NormalBalance.ToString(),
            result.Currency,
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.OpeningBalance,
            result.Items.Select(line => new AccountLedgerLineResponse(
                line.JournalEntryId,
                line.PostedAt,
                line.Description,
                line.ReversesJournalEntryId,
                line.LineNumber,
                line.Side.ToString(),
                line.Amount,
                line.Currency,
                line.BalanceAfter)).ToArray()));
    }
}

internal sealed record AccountLedgerResponse(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    string AccountType,
    string NormalBalance,
    string Currency,
    int Page,
    int PageSize,
    int TotalCount,
    decimal OpeningBalance,
    IReadOnlyList<AccountLedgerLineResponse> Items);

internal sealed record AccountLedgerLineResponse(
    Guid JournalEntryId,
    DateTimeOffset PostedAt,
    string Description,
    Guid? ReversesJournalEntryId,
    int LineNumber,
    string Side,
    decimal Amount,
    string Currency,
    decimal BalanceAfter);
