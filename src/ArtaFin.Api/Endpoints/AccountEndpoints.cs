using ArtaFin.Application.Features.Accounts.CreateAccount;
using ArtaFin.Application.Features.Accounts.DeleteAccount;
using ArtaFin.Application.Features.Accounts.GetAccount;
using ArtaFin.Application.Features.Accounts.UpdateAccount;
using ArtaFin.Domain.Finance;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

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

        endpoints.MapPut(
                "/api/financial-books/{financialBookId:guid}/accounts/{accountId:guid}",
                UpdateAsync)
            .WithName("UpdateAccount")
            .WithTags("Accounts")
            .RequireAuthorization()
            .Produces<AccountResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        endpoints.MapDelete(
                "/api/financial-books/{financialBookId:guid}/accounts/{accountId:guid}",
                DeleteAsync)
            .WithName("DeleteAccount")
            .WithTags("Accounts")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

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

        SetEtag(httpContext, snapshot.RowVersion);

        var account = snapshot.Account;
        return Results.Ok(ToResponse(account));
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

    private static async Task<IResult> UpdateAsync(
        Guid financialBookId,
        Guid accountId,
        UpdateAccountRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            request.Name.Trim().Length > Account.MaxNameLength)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = [string.IsNullOrWhiteSpace(request.Name)
                    ? "Account name is required."
                    : $"Account name cannot exceed {Account.MaxNameLength} characters."]
            });
        }

        if (string.IsNullOrWhiteSpace(ifMatch))
            return Results.Problem(
                statusCode: StatusCodes.Status428PreconditionRequired,
                title: "If-Match header required",
                detail: "Supply the account ETag returned by GET, or use If-Match: *.");

        if (!EntityTagHeaderValue.TryParseList(
                [ifMatch],
                out var entityTags) ||
            entityTags is null ||
            entityTags.Count == 0)
        {
            return Results.BadRequest(new
            {
                title = "Malformed If-Match header",
                detail = "The If-Match header must contain valid entity tags."
            });
        }

        var matchAny = entityTags.Any(tag => tag.Tag.Value == "*");
        if (matchAny && entityTags.Count != 1)
        {
            return Results.BadRequest(new
            {
                title = "Malformed If-Match header",
                detail = "The wildcard entity tag cannot be combined with other tags."
            });
        }

        var expectedVersions = entityTags
            .Where(tag => !tag.IsWeak && tag.Tag.Value != "*")
            .Select(tag =>
            {
                var tagValue = tag.Tag.Value ?? string.Empty;
                var encoded = tagValue.Length >= 2 &&
                              tagValue[0] == '"' &&
                              tagValue[^1] == '"'
                    ? tagValue[1..^1]
                    : tagValue;

                return Convert.TryFromBase64String(
                    encoded,
                    new byte[encoded.Length],
                    out var bytesWritten)
                    ? Convert.FromBase64String(encoded)
                    : null;
            })
            .Where(version => version is not null)
            .Select(version => version!)
            .ToArray();

        var snapshot = await sender.Send(
            new UpdateAccountCommand(
                financialBookId,
                accountId,
                request.Name,
                expectedVersions,
                matchAny),
            cancellationToken);

        if (snapshot is null)
            return Results.NotFound();

        SetEtag(httpContext, snapshot.RowVersion);
        return Results.Ok(ToResponse(snapshot.Account));
    }

    private static async Task<IResult> DeleteAsync(
        Guid financialBookId,
        Guid accountId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ifMatch))
            return Results.Problem(
                statusCode: StatusCodes.Status428PreconditionRequired,
                title: "If-Match header required",
                detail: "Supply the account ETag returned by GET, or use If-Match: *.");

        if (!EntityTagHeaderValue.TryParseList([ifMatch], out var entityTags) ||
            entityTags is null || entityTags.Count == 0)
        {
            return Results.BadRequest(new
            {
                title = "Malformed If-Match header",
                detail = "The If-Match header must contain valid entity tags."
            });
        }

        var matchAny = entityTags.Any(tag => tag.Tag.Value == "*");
        if (matchAny && entityTags.Count != 1)
        {
            return Results.BadRequest(new
            {
                title = "Malformed If-Match header",
                detail = "The wildcard entity tag cannot be combined with other tags."
            });
        }

        var expectedVersions = entityTags
            .Where(tag => !tag.IsWeak && tag.Tag.Value != "*")
            .Select(tag =>
            {
                var tagValue = tag.Tag.Value ?? string.Empty;
                var encoded = tagValue.Length >= 2 &&
                              tagValue[0] == '"' &&
                              tagValue[^1] == '"'
                    ? tagValue[1..^1]
                    : tagValue;

                return Convert.TryFromBase64String(
                    encoded,
                    new byte[encoded.Length],
                    out _)
                    ? Convert.FromBase64String(encoded)
                    : null;
            })
            .Where(version => version is not null)
            .Select(version => version!)
            .ToArray();

        var deleted = await sender.Send(
            new DeleteAccountCommand(
                financialBookId,
                accountId,
                expectedVersions,
                matchAny),
            cancellationToken);

        return deleted ? Results.NoContent() : Results.NotFound();
    }

    private static void SetEtag(HttpContext httpContext, byte[] rowVersion)
    {
        httpContext.Response.Headers["ETag"] = string.Concat(
            (char)34,
            Convert.ToBase64String(rowVersion),
            (char)34);
    }

    private static AccountResponse ToResponse(Account account) =>
        new(
            account.Id,
            account.FinancialBookId,
            account.Code,
            account.Name,
            account.Type.ToString(),
            account.NormalBalance.ToString(),
            account.Status.ToString(),
            account.CreatedAt,
            account.DeactivatedAt);
}

internal sealed record CreateAccountRequest(string? Code, string? Name, string? Type);
internal sealed record UpdateAccountRequest(string Name);
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
