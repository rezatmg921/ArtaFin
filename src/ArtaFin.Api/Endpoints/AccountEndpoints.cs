using ArtaFin.Application.Features.Accounts.CreateAccount;
using ArtaFin.Application.Features.Accounts.GetAccount;
using ArtaFin.Domain.Finance;
using MediatR;

namespace ArtaFin.Api.Endpoints;

internal static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/financial-books/{financialBookId:guid}/accounts/{accountId:guid}",
                GetByIdAsync)
            .WithName("GetAccount")
            .WithTags("Accounts")
            .RequireAuthorization()
            .Produces<AccountResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        endpoints.MapPost(
                "/api/financial-books/{financialBookId:guid}/accounts",
                CreateAsync)
            .WithName("CreateAccount")
            .WithTags("Accounts")
            .RequireAuthorization()
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> GetByIdAsync(
        Guid financialBookId,
        Guid accountId,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var snapshot = await sender.Send(
            new GetAccountQuery(financialBookId, accountId),
            cancellationToken);

        if (snapshot is null)
            return Results.NotFound();

        httpContext.Response.Headers["ETag"] =
            $"\\"{Convert.ToBase64String(snapshot.RowVersion)}\\"";

        var account = snapshot.Account;
        return Results.Ok(new AccountResponse(
            account.Id,
            account.FinancialBookId,
            account.Code,
            account.Name,
            account.Type.ToString(),
            account.NormalBalance.ToString(),
            account.Status.ToString(),
            account.CreatedAt,
            account.DeactivatedAt));
    }

    private static async Task<IResult> CreateAsync(
        Guid financialBookId,
        CreateAccountRequest request,
        CreateAccountCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Code))
            errors["code"] = ["Account code is required."];
        else if (request.Code.Trim().Length > Account.MaxCodeLength)
            errors["code"] = [$"Account code cannot exceed {Account.MaxCodeLength} characters."];

        if (string.IsNullOrWhiteSpace(request.Name))
            errors["name"] = ["Account name is required."];
        else if (request.Name.Trim().Length > Account.MaxNameLength)
            errors["name"] = [$"Account name cannot exceed {Account.MaxNameLength} characters."];

        if (!Enum.TryParse<AccountType>(request.Type, true, out var accountType) ||
            !Enum.IsDefined(accountType))
        {
            errors["type"] = ["Account type must be Asset, Liability, Equity, Income, or Expense."];
        }

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var command = new CreateAccountCommand(
            financialBookId,
            request.Code!,
            request.Name!,
            accountType);

        var accountId = await handler.HandleAsync(command, cancellationToken);
        var location = $"/api/financial-books/{financialBookId}/accounts/{accountId}";

        return Results.Created(location, new CreateAccountResponse(accountId));
    }
}

internal sealed record CreateAccountRequest(string? Code, string? Name, string? Type);
internal sealed record CreateAccountResponse(Guid Id);

internal sealed record AccountResponse(
    Guid Id,
    Guid FinancialBookId,
    string Code,
    string Name,
    string Type,
    string NormalBalance,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DeactivatedAt);
