using ArtaFin.Application.Features.Accounts.CreateAccount;
using ArtaFin.Domain.Finance;

namespace ArtaFin.Api.Endpoints;

internal static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
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
