using ArtaFin.Application.Features.Obligations;
using ArtaFin.Domain.Obligations;

namespace ArtaFin.Api.Endpoints;

internal static class FinancialObligationEndpoints
{
    public static IEndpointRouteBuilder MapFinancialObligationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/financial-books/{financialBookId:guid}/obligations", ListAsync)
            .WithName("ListFinancialObligations").WithTags("Financial Obligations")
            .RequireAuthorization("Permission:Obligations.Read")
            .Produces<IReadOnlyList<ObligationResponse>>();

        endpoints.MapGet("/api/financial-books/{financialBookId:guid}/obligations/{obligationId:guid}", GetAsync)
            .WithName("GetFinancialObligation").WithTags("Financial Obligations")
            .RequireAuthorization("Permission:Obligations.Read")
            .Produces<ObligationResponse>().Produces(StatusCodes.Status404NotFound);

        endpoints.MapPost("/api/financial-books/{financialBookId:guid}/obligations", CreateAsync)
            .WithName("CreateFinancialObligation").WithTags("Financial Obligations")
            .RequireAuthorization("Permission:Obligations.Create")
            .Produces<CreateObligationResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem().Produces(StatusCodes.Status403Forbidden);

        endpoints.MapPost("/api/financial-books/{financialBookId:guid}/obligations/{obligationId:guid}/cancel", CancelAsync)
            .WithName("CancelFinancialObligation").WithTags("Financial Obligations")
            .RequireAuthorization("Permission:Obligations.Cancel")
            .Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid financialBookId, string? direction, string? status, int? skip, int? take,
        FinancialObligationService service, CancellationToken cancellationToken)
    {
        if (!TryParseOptional(direction, out FinancialObligationDirection? parsedDirection) ||
            !TryParseOptional(status, out FinancialObligationStatus? parsedStatus))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["filter"] = ["Direction or status contains an unsupported value."]
            });
        var pageSkip = skip ?? 0;
        var pageTake = take ?? 50;
        if (pageSkip < 0 || pageTake is < 1 or > 100)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["pagination"] = ["Skip must be zero or greater and take must be between 1 and 100."]
            });
        var items = await service.ListAsync(financialBookId, parsedDirection, parsedStatus,
            pageSkip, pageTake, cancellationToken);
        return Results.Ok(items.Select(ToResponse).ToArray());
    }

    private static async Task<IResult> GetAsync(
        Guid financialBookId, Guid obligationId, FinancialObligationService service,
        CancellationToken cancellationToken)
    {
        var item = await service.GetByIdAsync(financialBookId, obligationId, cancellationToken);
        return item is null ? Results.NotFound() : Results.Ok(ToResponse(item));
    }

    private static async Task<IResult> CreateAsync(
        Guid financialBookId, CreateObligationRequest request,
        FinancialObligationService service, CancellationToken cancellationToken)
    {
        var errors = Validate(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var id = await service.CreateAsync(financialBookId, request.ContactId, request.ControlAccountId,
            request.Direction, request.Kind, request.Amount, request.Currency!,
            request.EffectiveDate, request.DueDate, request.Description!, request.Reference,
            cancellationToken, request.Installments?.Select(x => new FinancialObligationInstallmentTerm(x.Amount, x.DueDate)).ToArray());
        return Results.Created($"/api/financial-books/{financialBookId}/obligations/{id}",
            new CreateObligationResponse(id));
    }

    private static async Task<IResult> CancelAsync(
        Guid financialBookId, Guid obligationId, FinancialObligationService service,
        CancellationToken cancellationToken)
    {
        await service.CancelAsync(financialBookId, obligationId, cancellationToken);
        return Results.NoContent();
    }

    private static Dictionary<string, string[]> Validate(CreateObligationRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.ContactId == Guid.Empty) errors["contactId"] = ["A contact is required."];
        if (request.ControlAccountId == Guid.Empty) errors["controlAccountId"] = ["A control account is required."];
        if (!Enum.IsDefined(request.Direction)) errors["direction"] = ["Direction must be Receivable or Payable."];
        if (!Enum.IsDefined(request.Kind)) errors["kind"] = ["Kind must be GeneralDebt or Loan."];
        if (request.Amount <= 0) errors["amount"] = ["Amount must be greater than zero."];
        if (string.IsNullOrWhiteSpace(request.Currency)) errors["currency"] = ["Currency is required."];
        if (string.IsNullOrWhiteSpace(request.Description)) errors["description"] = ["Description is required."];
        if (request.Installments is { Length: 0 }) errors["installments"] = ["When supplied, installments must contain at least one item."];
        if (request.Installments is { Length: > FinancialObligation.MaxInstallments }) errors["installments"] = [$"No more than {FinancialObligation.MaxInstallments} installments are allowed."];
        if (request.Installments is not null && request.Installments.Any(x => x.Amount <= 0)) errors["installments"] = ["Every installment amount must be greater than zero."];
        return errors;
    }

    private static bool TryParseOptional<TEnum>(string? value, out TEnum? result)
        where TEnum : struct, Enum
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (!Enum.TryParse<TEnum>(value, true, out var parsed) || !Enum.IsDefined(parsed)) return false;
        result = parsed;
        return true;
    }

    private static ObligationResponse ToResponse(FinancialObligationSnapshot x) => new(
        x.Id, x.FinancialBookId, x.ContactId, x.ControlAccountId, x.Direction.ToString(),
        x.Kind.ToString(), x.OriginalAmount, x.Currency, x.EffectiveDate, x.DueDate,
        x.Description, x.Reference, x.Status.ToString(), x.CreatedAt, x.UpdatedAt, x.CancelledAt,
        x.Installments.Select(i => new InstallmentResponse(i.Id, i.InstallmentNumber, i.DueDate, i.Amount)).ToArray());

    private sealed record CreateObligationRequest(
        Guid ContactId, Guid ControlAccountId, FinancialObligationDirection Direction,
        FinancialObligationKind Kind, decimal Amount, string? Currency,
        DateOnly EffectiveDate, DateOnly DueDate, string? Description, string? Reference,
        InstallmentRequest[]? Installments = null);
    private sealed record InstallmentRequest(decimal Amount, DateOnly DueDate);
    private sealed record InstallmentResponse(Guid Id, int InstallmentNumber, DateOnly DueDate, decimal Amount);
    private sealed record CreateObligationResponse(Guid Id);
    private sealed record ObligationResponse(
        Guid Id, Guid FinancialBookId, Guid ContactId, Guid ControlAccountId,
        string Direction, string Kind, decimal Amount, string Currency,
        DateOnly EffectiveDate, DateOnly DueDate, string Description, string? Reference,
        string Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? CancelledAt,
        IReadOnlyList<InstallmentResponse> Installments);
}
