using ArtaFin.Application.Features.CashFlowAllocations;
using ArtaFin.Domain.Finance;

namespace ArtaFin.Api.Endpoints;

internal static class CashFlowAllocationEndpoints
{
    public static IEndpointRouteBuilder MapCashFlowAllocationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/api/financial-books/{financialBookId:guid}/journal-entries/{journalEntryId:guid}/lines/{lineNumber:int}/cash-flow-allocations",
                CreateDraftAsync)
            .WithName("CreateCashFlowAllocationDraft")
            .WithTags("CashFlowAllocations")
            .RequireAuthorization()
            .Produces<CashFlowAllocationSetResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        endpoints.MapGet(
                "/api/financial-books/{financialBookId:guid}/cash-flow-allocations/{allocationSetId:guid}",
                GetAsync)
            .WithName("GetCashFlowAllocationSet")
            .WithTags("CashFlowAllocations")
            .RequireAuthorization()
            .Produces<CashFlowAllocationSetResult>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        endpoints.MapPost(
                "/api/financial-books/{financialBookId:guid}/cash-flow-allocations/{allocationSetId:guid}/items",
                AddAllocationAsync)
            .WithName("AddCashFlowAllocation")
            .WithTags("CashFlowAllocations")
            .RequireAuthorization()
            .Produces<CashFlowAllocationSetResult>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        endpoints.MapDelete(
                "/api/financial-books/{financialBookId:guid}/cash-flow-allocations/{allocationSetId:guid}/items/{allocationId:guid}",
                RemoveAllocationAsync)
            .WithName("RemoveCashFlowAllocation")
            .WithTags("CashFlowAllocations")
            .RequireAuthorization()
            .Produces<CashFlowAllocationSetResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        endpoints.MapPost(
                "/api/financial-books/{financialBookId:guid}/cash-flow-allocations/{allocationSetId:guid}/finalize",
                FinalizeAsync)
            .WithName("FinalizeCashFlowAllocationSet")
            .WithTags("CashFlowAllocations")
            .RequireAuthorization()
            .Produces<CashFlowAllocationSetResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        endpoints.MapPost(
                "/api/financial-books/{financialBookId:guid}/cash-flow-allocations/{allocationSetId:guid}/revision",
                CreateRevisionAsync)
            .WithName("CreateCashFlowAllocationRevision")
            .WithTags("CashFlowAllocations")
            .RequireAuthorization()
            .Produces<CashFlowAllocationSetResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> CreateDraftAsync(
        Guid financialBookId,
        Guid journalEntryId,
        int lineNumber,
        CashFlowAllocationService service,
        CancellationToken cancellationToken)
    {
        if (lineNumber <= 0)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["lineNumber"] = ["Line number must be greater than zero."]
            });

        var result = await service.CreateDraftAsync(
            financialBookId, journalEntryId, lineNumber, cancellationToken);

        var location = $"/api/financial-books/{financialBookId}/cash-flow-allocations/{result.Id}";
        return Results.Created(location, result);
    }

    private static async Task<IResult> GetAsync(
        Guid financialBookId,
        Guid allocationSetId,
        CashFlowAllocationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(financialBookId, allocationSetId, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> AddAllocationAsync(
        Guid financialBookId,
        Guid allocationSetId,
        AddCashFlowAllocationRequest request,
        CashFlowAllocationService service,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Amount <= 0)
            errors["amount"] = ["Amount must be greater than zero."];
        if (!Enum.TryParse<CashFlowActivity>(request.Activity, true, out var activity) ||
            !Enum.IsDefined(activity))
            errors["activity"] = ["Activity must be Operating, Investing, or Financing."];
        if (string.IsNullOrWhiteSpace(request.Category) ||
            request.Category.Trim().Length > CashFlowAllocationSet.MaxCategoryLength)
            errors["category"] = [$"Category is required and cannot exceed {CashFlowAllocationSet.MaxCategoryLength} characters."];
        if (request.CounterpartLineNumber is null or <= 0)
            errors["counterpartLineNumber"] = ["A positive counterpart line number is required."];
        if (request.Memo?.Length > CashFlowAllocationSet.MaxMemoLength)
            errors["memo"] = [$"Memo cannot exceed {CashFlowAllocationSet.MaxMemoLength} characters."];

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var result = await service.AddAllocationAsync(
            financialBookId,
            allocationSetId,
            new AddCashFlowAllocationInput(
                request.Amount, activity, request.Category!, request.CounterpartLineNumber, request.Memo),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> RemoveAllocationAsync(
        Guid financialBookId,
        Guid allocationSetId,
        Guid allocationId,
        CashFlowAllocationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.RemoveAllocationAsync(
            financialBookId, allocationSetId, allocationId, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> FinalizeAsync(
        Guid financialBookId,
        Guid allocationSetId,
        CashFlowAllocationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.FinalizeAsync(
            financialBookId, allocationSetId, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateRevisionAsync(
        Guid financialBookId,
        Guid allocationSetId,
        CashFlowAllocationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateRevisionAsync(
            financialBookId, allocationSetId, cancellationToken);
        var location = $"/api/financial-books/{financialBookId}/cash-flow-allocations/{result.Id}";
        return Results.Created(location, result);
    }
}

internal sealed record AddCashFlowAllocationRequest(
    decimal Amount,
    string? Activity,
    string? Category,
    int? CounterpartLineNumber,
    string? Memo);
