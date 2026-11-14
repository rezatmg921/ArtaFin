using ArtaFin.Application.Features.FinancialBooks.CreateFinancialBook;
using ArtaFin.Domain.Finance;

namespace ArtaFin.Api.Endpoints;

internal static class FinancialBookEndpoints
{
    public static IEndpointRouteBuilder MapFinancialBookEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/financial-books", CreateAsync)
            .WithName("CreateFinancialBook")
            .WithTags("FinancialBooks")
            .RequireAuthorization()
            .Produces<CreateFinancialBookResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        CreateFinancialBookRequest request,
        CreateFinancialBookCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Name))
            errors["name"] = ["Financial book name is required."];
        else if (request.Name.Trim().Length > FinancialBook.MaxNameLength)
            errors["name"] = [$"Financial book name cannot exceed {FinancialBook.MaxNameLength} characters."];

        if (string.IsNullOrWhiteSpace(request.BaseCurrency))
        {
            errors["baseCurrency"] = ["Base currency is required."];
        }
        else
        {
            try
            {
                _ = Currency.FromCode(request.BaseCurrency);
            }
            catch (ArgumentException exception)
            {
                errors["baseCurrency"] = [exception.Message];
            }
            catch (NotSupportedException exception)
            {
                errors["baseCurrency"] = [exception.Message];
            }
        }

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var id = await handler.HandleAsync(
            new CreateFinancialBookCommand(request.Name!, request.BaseCurrency!),
            cancellationToken);

        return Results.Created(
            (string?)null,
            new CreateFinancialBookResponse(id));
    }
}

internal sealed record CreateFinancialBookRequest(string? Name, string? BaseCurrency);
internal sealed record CreateFinancialBookResponse(Guid Id);
