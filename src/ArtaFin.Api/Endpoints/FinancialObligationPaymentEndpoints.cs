using ArtaFin.Application.Features.ObligationPayments;
using ArtaFin.Domain.Finance;

namespace ArtaFin.Api.Endpoints;

internal static class FinancialObligationPaymentEndpoints
{
    public static IEndpointRouteBuilder MapFinancialObligationPaymentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/api/financial-books/{financialBookId:guid}/obligations/{obligationId:guid}/payments",
                CreateAsync)
            .WithName("CreateFinancialObligationPayment")
            .WithTags("Financial Obligation Payments")
            .RequireAuthorization("Permission:ObligationPayments.Create")
            .Produces<CreateObligationPaymentResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        endpoints.MapGet(
                "/api/financial-books/{financialBookId:guid}/obligations/{obligationId:guid}/payments",
                ListAsync)
            .WithName("ListFinancialObligationPayments")
            .WithTags("Financial Obligation Payments")
            .RequireAuthorization("Permission:ObligationPayments.Read")
            .Produces<IReadOnlyList<ObligationPaymentResponse>>();

        endpoints.MapGet(
                "/api/financial-books/{financialBookId:guid}/obligation-payments/{paymentId:guid}",
                GetAsync)
            .WithName("GetFinancialObligationPayment")
            .WithTags("Financial Obligation Payments")
            .RequireAuthorization("Permission:ObligationPayments.Read")
            .Produces<ObligationPaymentResponse>()
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        Guid financialBookId, Guid obligationId, CreateObligationPaymentRequest request,
        FinancialObligationPaymentService service, HttpRequest httpRequest,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var key = IdempotencyKeyHeader.Read(httpRequest, errors);
        if (key is null && !errors.ContainsKey("Idempotency-Key"))
            errors["Idempotency-Key"] = ["Idempotency-Key header is required."];

        if (request.CashAccountId == Guid.Empty)
            errors["cashAccountId"] = ["Cash or bank account is required."];
        if (request.Amount <= 0)
            errors["amount"] = ["Amount must be greater than zero."];
        if (string.IsNullOrWhiteSpace(request.Currency))
            errors["currency"] = ["Currency is required."];
        else
        {
            try { _ = new Money(request.Amount, request.Currency); }
            catch (ArgumentException exception) { errors["currency"] = [exception.Message]; }
            catch (NotSupportedException exception) { errors["currency"] = [exception.Message]; }
        }
        if (string.IsNullOrWhiteSpace(request.Description))
            errors["description"] = ["Description is required."];
        if (request.Allocations is null || request.Allocations.Length == 0)
            errors["allocations"] = ["At least one installment allocation is required."];
        else if (request.Allocations.Length > 1200)
            errors["allocations"] = ["No more than 1200 allocations are allowed."];
        else if (request.Allocations.Any(x => x.InstallmentId == Guid.Empty || x.Amount <= 0))
            errors["allocations"] = ["Every allocation requires an installment and a positive amount."];

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var result = await service.CreateAsync(
            financialBookId, obligationId, request.CashAccountId, request.Amount,
            request.Currency!, request.PaymentDate, request.Description!, request.Reference,
            key!, request.Allocations!.Select(x =>
                new ObligationPaymentAllocationRequest(x.InstallmentId, x.Amount)).ToArray(),
            cancellationToken);

        var response = new CreateObligationPaymentResponse(
            result.PaymentId, result.JournalEntryId, result.WasAlreadyCreated);
        return result.WasAlreadyCreated
            ? Results.Ok(response)
            : Results.Created(
                $"/api/financial-books/{financialBookId}/obligation-payments/{result.PaymentId}",
                response);
    }

    private static async Task<IResult> ListAsync(
        Guid financialBookId, Guid obligationId, int? skip, int? take,
        FinancialObligationPaymentService service, CancellationToken cancellationToken)
    {
        var pageSkip = skip ?? 0;
        var pageTake = take ?? 50;
        if (pageSkip < 0 || pageTake is < 1 or > 100)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["pagination"] = ["Skip must be zero or greater and take must be between 1 and 100."]
            });

        var items = await service.ListByObligationAsync(
            financialBookId, obligationId, pageSkip, pageTake, cancellationToken);
        return Results.Ok(items.Select(ToResponse).ToArray());
    }

    private static async Task<IResult> GetAsync(
        Guid financialBookId, Guid paymentId,
        FinancialObligationPaymentService service, CancellationToken cancellationToken)
    {
        var item = await service.GetByIdAsync(financialBookId, paymentId, cancellationToken);
        return item is null ? Results.NotFound() : Results.Ok(ToResponse(item));
    }

    private static ObligationPaymentResponse ToResponse(ObligationPaymentSnapshot x) => new(
        x.Id, x.FinancialBookId, x.FinancialObligationId, x.CashAccountId,
        x.JournalEntryId, x.Amount, x.Currency, x.PaymentDate, x.Description,
        x.Reference, x.CreatedBy, x.CreatedAt,
        x.Allocations.Select(a => new PaymentAllocationResponse(a.InstallmentId, a.Amount)).ToArray());

    private sealed record CreateObligationPaymentRequest(
        Guid CashAccountId, decimal Amount, string? Currency, DateOnly PaymentDate,
        string? Description, string? Reference, PaymentAllocationRequest[]? Allocations);
    private sealed record PaymentAllocationRequest(Guid InstallmentId, decimal Amount);
    private sealed record PaymentAllocationResponse(Guid InstallmentId, decimal Amount);
    private sealed record CreateObligationPaymentResponse(Guid PaymentId, Guid JournalEntryId, bool WasAlreadyCreated);
    private sealed record ObligationPaymentResponse(
        Guid Id, Guid FinancialBookId, Guid FinancialObligationId, Guid CashAccountId,
        Guid JournalEntryId, decimal Amount, string Currency, DateOnly PaymentDate,
        string Description, string? Reference, Guid CreatedBy, DateTimeOffset CreatedAt,
        IReadOnlyList<PaymentAllocationResponse> Allocations);
}
