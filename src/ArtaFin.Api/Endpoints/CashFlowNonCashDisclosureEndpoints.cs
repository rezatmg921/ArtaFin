using ArtaFin.Application.Features.CashFlowNonCashDisclosures;
using ArtaFin.Domain.Finance;

namespace ArtaFin.Api.Endpoints;

internal static class CashFlowNonCashDisclosureEndpoints
{
    public static IEndpointRouteBuilder MapCashFlowNonCashDisclosureEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/financial-books/{financialBookId:guid}/journal-entries/{journalEntryId:guid}/non-cash-disclosures",
                GetByJournalEntryAsync)
            .WithName("GetCashFlowNonCashDisclosures")
            .WithTags("CashFlowNonCashDisclosures")
            .RequireAuthorization("Permission:Reports.Read")
            .Produces<IReadOnlyList<CashFlowNonCashDisclosureResult>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        endpoints.MapPost(
                "/api/financial-books/{financialBookId:guid}/journal-entries/{journalEntryId:guid}/lines/{lineNumber:int}/non-cash-disclosures",
                CreateAsync)
            .WithName("CreateCashFlowNonCashDisclosure")
            .WithTags("CashFlowNonCashDisclosures")
            .RequireAuthorization("Permission:Transactions.Create")
            .Produces<CashFlowNonCashDisclosureResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> GetByJournalEntryAsync(
        Guid financialBookId,
        Guid journalEntryId,
        CashFlowNonCashDisclosureService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetByJournalEntryAsync(
            financialBookId, journalEntryId, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateAsync(
        Guid financialBookId,
        Guid journalEntryId,
        int lineNumber,
        CreateCashFlowNonCashDisclosureRequest request,
        CashFlowNonCashDisclosureService service,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.Amount <= 0)
            errors["amount"] = ["Amount must be greater than zero."];

        if (string.IsNullOrWhiteSpace(request.Currency) ||
            request.Currency.Trim().Length != 3 ||
            !request.Currency.Trim().All(char.IsAsciiLetter))
            errors["currency"] = ["Currency must be a three-letter currency code."];

        if (!Enum.TryParse<CashFlowActivity>(request.Activity, true, out var activity) ||
            !Enum.IsDefined(activity) ||
            activity is not CashFlowActivity.Investing and not CashFlowActivity.Financing)
            errors["activity"] = ["Activity must be Investing or Financing."];

        if (!IsCategoryCode(request.Category))
            errors["category"] = [$"Category must be a stable ASCII code of at most {CashFlowNonCashDisclosure.MaxCategoryLength} characters."];

        if (request.Memo?.Length > CashFlowNonCashDisclosure.MaxMemoLength)
            errors["memo"] = [$"Memo cannot exceed {CashFlowNonCashDisclosure.MaxMemoLength} characters."];

        if (lineNumber <= 0)
            errors["lineNumber"] = ["Line number must be greater than zero."];

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var result = await service.CreateAsync(
            financialBookId,
            journalEntryId,
            lineNumber,
            new CreateCashFlowNonCashDisclosureInput(
                request.Amount, request.Currency!, activity, request.Category!, request.Memo),
            cancellationToken);

        var location = $"/api/financial-books/{financialBookId}/journal-entries/{journalEntryId}/non-cash-disclosures";
        return Results.Created(location, result);
    }

    private static bool IsCategoryCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var category = value.Trim();
        return category.Length <= CashFlowNonCashDisclosure.MaxCategoryLength &&
               char.IsAsciiLetter(category[0]) &&
               category.All(character =>
                   char.IsAsciiLetterOrDigit(character) || character == '_');
    }
}

internal sealed record CreateCashFlowNonCashDisclosureRequest(
    decimal Amount,
    string? Currency,
    string? Activity,
    string? Category,
    string? Memo);
