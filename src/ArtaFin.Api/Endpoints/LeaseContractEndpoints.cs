using ArtaFin.Application.Features.LeaseAccounting;
using ArtaFin.Domain.Finance;

namespace ArtaFin.Api.Endpoints;

internal static class LeaseContractEndpoints
{
    public static IEndpointRouteBuilder MapLeaseContractEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/financial-books/{financialBookId:guid}/lease-contracts",
                GetByFinancialBookAsync)
            .WithName("GetLeaseContracts")
            .WithTags("LeaseAccounting")
            .RequireAuthorization()
            .Produces<IReadOnlyList<LeaseContractResult>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        endpoints.MapGet("/api/financial-books/{financialBookId:guid}/lease-contracts/{leaseContractId:guid}",
                GetByIdAsync)
            .WithName("GetLeaseContract")
            .WithTags("LeaseAccounting")
            .RequireAuthorization()
            .Produces<LeaseContractResult>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        endpoints.MapPost("/api/financial-books/{financialBookId:guid}/lease-contracts",
                CreateAsync)
            .WithName("CreateLeaseContract")
            .WithTags("LeaseAccounting")
            .RequireAuthorization()
            .Produces<LeaseContractResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> GetByFinancialBookAsync(
        Guid financialBookId,
        LeaseContractService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetByFinancialBookAsync(financialBookId, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetByIdAsync(
        Guid financialBookId,
        Guid leaseContractId,
        LeaseContractService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(financialBookId, leaseContractId, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> CreateAsync(
        Guid financialBookId,
        CreateLeaseContractRequest request,
        LeaseContractService service,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.ContractNumber) ||
            request.ContractNumber.Trim().Length > LeaseContract.MaxContractNumberLength)
            errors["contractNumber"] = ["Contract number is required and cannot exceed 100 characters."];

        if (string.IsNullOrWhiteSpace(request.Description) ||
            request.Description.Trim().Length > LeaseContract.MaxDescriptionLength)
            errors["description"] = ["Description is required and cannot exceed 500 characters."];

        if (request.InitialLiabilityAmount <= 0m)
            errors["initialLiabilityAmount"] = ["Initial liability amount must be greater than zero."];

        if (request.EffectivePeriodicRate < 0m || request.EffectivePeriodicRate > LeaseContract.MaxEffectivePeriodicRate)
            errors["effectivePeriodicRate"] = ["Effective periodic rate must fit decimal(19,10) and cannot be negative."];

        if (string.IsNullOrWhiteSpace(request.Currency) ||
            request.Currency.Trim().Length != 3 ||
            !request.Currency.Trim().All(char.IsAsciiLetter))
            errors["currency"] = ["Currency must be a three-letter ISO currency code."];

        if (!Enum.TryParse<LeasePaymentFrequency>(request.PaymentFrequency, true, out var frequency) ||
            !Enum.IsDefined(frequency))
            errors["paymentFrequency"] = ["Payment frequency must be Monthly, Quarterly, SemiAnnual, or Annual."];

        if (!Enum.TryParse<LeasePaymentTiming>(request.PaymentTiming, true, out var timing) ||
            !Enum.IsDefined(timing))
            errors["paymentTiming"] = ["Payment timing must be InAdvance or InArrears."];

        if (request.CommencementDate == default || request.FirstPaymentDate == default ||
            request.ContractEndDate == default)
            errors["dates"] = ["Commencement, first payment, and contract end dates are required."];
        else if (request.FirstPaymentDate < request.CommencementDate ||
                 (timing == LeasePaymentTiming.InArrears && request.FirstPaymentDate == request.CommencementDate))
            errors["firstPaymentDate"] = ["First payment date is inconsistent with the commencement date and payment timing."];
        else if (request.ContractEndDate < request.FirstPaymentDate)
            errors["contractEndDate"] = ["Contract end date cannot precede the first payment date."];

        if (request.LeaseLiabilityAccountId == Guid.Empty)
            errors["leaseLiabilityAccountId"] = ["Lease liability account is required."];
        if (request.InterestExpenseAccountId == Guid.Empty)
            errors["interestExpenseAccountId"] = ["Interest expense account is required."];
        if (request.PaymentAccountId == Guid.Empty)
            errors["paymentAccountId"] = ["Payment account is required."];
        if (request.LeaseLiabilityAccountId != Guid.Empty &&
            (request.LeaseLiabilityAccountId == request.InterestExpenseAccountId ||
             request.LeaseLiabilityAccountId == request.PaymentAccountId ||
             request.InterestExpenseAccountId == request.PaymentAccountId))
            errors["accounts"] = ["Lease liability, interest expense, and payment accounts must be different."];

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        try
        {
            _ = new Money(request.InitialLiabilityAmount, request.Currency!);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["currency"] = [exception.Message]
            });
        }

        var result = await service.CreateAsync(
            financialBookId,
            new CreateLeaseContractInput(
                request.ContractNumber!,
                request.Description!,
                request.CommencementDate,
                request.FirstPaymentDate,
                request.ContractEndDate,
                request.InitialLiabilityAmount,
                request.Currency!,
                request.EffectivePeriodicRate,
                frequency,
                timing,
                request.LeaseLiabilityAccountId,
                request.InterestExpenseAccountId,
                request.PaymentAccountId),
            cancellationToken);

        return Results.Created(
            $"/api/financial-books/{financialBookId}/lease-contracts/{result.Id}",
            result);
    }
}

internal sealed record CreateLeaseContractRequest(
    string? ContractNumber,
    string? Description,
    DateOnly CommencementDate,
    DateOnly FirstPaymentDate,
    DateOnly ContractEndDate,
    decimal InitialLiabilityAmount,
    string? Currency,
    decimal EffectivePeriodicRate,
    string? PaymentFrequency,
    string? PaymentTiming,
    Guid LeaseLiabilityAccountId,
    Guid InterestExpenseAccountId,
    Guid PaymentAccountId);
