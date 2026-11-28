using ArtaFin.Application.Features.LeaseAccounting;
using ArtaFin.Domain.Finance;

namespace ArtaFin.Api.Endpoints;

internal static class LeasePaymentScheduleEndpoints
{
    public static IEndpointRouteBuilder MapLeasePaymentScheduleEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/financial-books/{financialBookId:guid}/lease-contracts/{leaseContractId:guid}/payment-schedules",
                GetByContractAsync)
            .WithName("GetLeasePaymentSchedules")
            .WithTags("LeaseAccounting")
            .RequireAuthorization("Permission:Leases.Read")
            .Produces<IReadOnlyList<LeasePaymentScheduleResult>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        endpoints.MapPost(
                "/api/financial-books/{financialBookId:guid}/lease-contracts/{leaseContractId:guid}/payment-schedules",
                CreateAsync)
            .WithName("CreateLeasePaymentSchedule")
            .WithTags("LeaseAccounting")
            .RequireAuthorization("Permission:Leases.Manage")
            .Produces<LeasePaymentScheduleResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        endpoints.MapPost(
                "/api/financial-books/{financialBookId:guid}/lease-contracts/{leaseContractId:guid}/payment-schedules/{scheduleId:guid}/finalize",
                FinalizeAsync)
            .WithName("FinalizeLeasePaymentSchedule")
            .WithTags("LeaseAccounting")
            .RequireAuthorization("Permission:Leases.Manage")
            .Produces<LeasePaymentScheduleResult>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> GetByContractAsync(
        Guid financialBookId,
        Guid leaseContractId,
        LeasePaymentScheduleService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetByContractAsync(
            financialBookId, leaseContractId, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateAsync(
        Guid financialBookId,
        Guid leaseContractId,
        CreateLeasePaymentScheduleRequest request,
        LeasePaymentScheduleService service,
        CancellationToken cancellationToken)
    {
        if (request.Payments is null || request.Payments.Count is < 1 or > LeasePaymentSchedule.MaxPayments)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["payments"] = [$"Between 1 and {LeasePaymentSchedule.MaxPayments} dated payments are required."]
            });

        var errors = new Dictionary<string, string[]>();
        for (var index = 0; index < request.Payments.Count; index++)
        {
            var payment = request.Payments[index];
            if (payment.PaymentDate == default)
                errors[$"payments[{index}].paymentDate"] = ["Payment date is required."];
            if (payment.PaymentAmount < 0m)
                errors[$"payments[{index}].paymentAmount"] = ["Payment amount cannot be negative."];
            if (payment.EffectivePeriodicRate < 0m ||
                payment.EffectivePeriodicRate > LeaseContract.MaxEffectivePeriodicRate)
                errors[$"payments[{index}].effectivePeriodicRate"] =
                    ["Effective periodic rate must fit decimal(19,10) and cannot be negative."];
        }

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var payments = request.Payments.Select(payment => new LeaseScheduledPayment(
            payment.PaymentDate,
            payment.PaymentAmount,
            payment.EffectivePeriodicRate)).ToArray();

        var result = await service.CreateAsync(
            financialBookId, leaseContractId, payments, cancellationToken);

        return Results.Created(
            $"/api/financial-books/{financialBookId}/lease-contracts/{leaseContractId}/payment-schedules",
            result);
    }

    private static async Task<IResult> FinalizeAsync(
        Guid financialBookId,
        Guid leaseContractId,
        Guid scheduleId,
        LeasePaymentScheduleService service,
        CancellationToken cancellationToken)
    {
        var result = await service.FinalizeAsync(
            financialBookId, leaseContractId, scheduleId, cancellationToken);
        return Results.Ok(result);
    }
}

internal sealed record CreateLeasePaymentScheduleRequest(
    IReadOnlyList<CreateLeasePaymentSchedulePaymentRequest>? Payments);

internal sealed record CreateLeasePaymentSchedulePaymentRequest(
    DateOnly PaymentDate,
    decimal PaymentAmount,
    decimal EffectivePeriodicRate);
