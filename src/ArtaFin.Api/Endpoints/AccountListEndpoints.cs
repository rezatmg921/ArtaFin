using ArtaFin.Application.Features.Accounts.ListAccounts;
using ArtaFin.Domain.Finance;
using MediatR;

namespace ArtaFin.Api.Endpoints;

internal static class AccountListEndpoints
{
    public static IEndpointRouteBuilder MapAccountListEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/financial-books/{financialBookId:guid}/accounts",
                ListAsync)
            .WithName("ListAccounts")
            .WithTags("Accounts")
            .RequireAuthorization()
            .Produces<AccountListResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid financialBookId,
        ISender sender,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 50,
        string? search = null,
        string? status = null)
    {
        AccountStatus? accountStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<AccountStatus>(status, true, out var parsedStatus) ||
                !Enum.IsDefined(parsedStatus))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = ["Status must be Active or Inactive."]
                });
            }

            accountStatus = parsedStatus;
        }

        if (page < 1)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["page"] = ["Page must be greater than zero."]
            });
        }

        if (pageSize is < 1 or > 100)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["pageSize"] = ["Page size must be between 1 and 100."]
            });
        }

        if (search is not null && search.Trim().Length > 200)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["search"] = ["Search term cannot exceed 200 characters."]
            });
        }

        var result = await sender.Send(
            new ListAccountsQuery(
                financialBookId,
                accountStatus,
                search,
                page,
                pageSize),
            cancellationToken);

        var response = new AccountListResponse(
            page,
            pageSize,
            result.TotalCount,
            result.Items.Select(account => new AccountListItemResponse(
                account.Id,
                account.Code,
                account.Name,
                account.Type.ToString(),
                account.NormalBalance.ToString(),
                account.Status.ToString(),
                account.CreatedAt,
                account.DeactivatedAt)).ToArray());

        return Results.Ok(response);
    }
}

internal sealed record AccountListResponse(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<AccountListItemResponse> Items);

internal sealed record AccountListItemResponse(
    Guid Id,
    string Code,
    string Name,
    string Type,
    string NormalBalance,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DeactivatedAt);
