using ArtaFin.Application.Features.Expenses.CreateExpense;
using ArtaFin.Application.Features.JournalEntries.CreateJournalEntry;
using ArtaFin.Domain.Finance;
using MediatR;

namespace ArtaFin.Api.Endpoints;

internal static class ExpenseEndpoints
{
    public static IEndpointRouteBuilder MapExpenseEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/api/financial-books/{financialBookId:guid}/expenses",
                CreateAsync)
            .WithName("CreateExpense")
            .WithTags("Expenses")
            .RequireAuthorization()
            .Produces<CreateExpenseResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        Guid financialBookId,
        CreateExpenseRequest request,
        ISender sender,
        HttpRequest httpRequest,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var idempotencyKey = IdempotencyKeyHeader.Read(httpRequest, errors);

        if (string.IsNullOrWhiteSpace(request.Description))
            errors["description"] = ["Description is required."];
        else if (request.Description.Trim().Length > JournalEntry.MaxDescriptionLength)
            errors["description"] = [$"Description cannot exceed {JournalEntry.MaxDescriptionLength} characters."];

        if (request.ExpenseAccountId == Guid.Empty)
            errors["expenseAccountId"] = ["Expense account id is required."];
        if (request.PaymentAccountId == Guid.Empty)
            errors["paymentAccountId"] = ["Payment account id is required."];
        if (request.ExpenseAccountId != Guid.Empty &&
            request.ExpenseAccountId == request.PaymentAccountId)
        {
            errors["paymentAccountId"] = ["Expense and payment accounts must be different."];
        }
        if (request.Amount <= 0)
            errors["amount"] = ["Amount must be greater than zero."];

        Money amount;
        try
        {
            amount = new Money(request.Amount, request.Currency ?? string.Empty);
        }
        catch (ArgumentException exception)
        {
            errors["currency"] = [exception.Message];
            amount = default;
        }
        catch (NotSupportedException exception)
        {
            errors["currency"] = [exception.Message];
            amount = default;
        }

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var entryId = await sender.Send(
            new CreateExpenseCommand(
                financialBookId,
                request.ExpenseAccountId,
                request.PaymentAccountId,
                request.Description!,
                amount,
                idempotencyKey),
            cancellationToken);

        var location = $"/api/financial-books/{financialBookId}/journal-entries/{entryId}";
        return Results.Created(location, new CreateExpenseResponse(entryId));
    }
}

internal sealed record CreateExpenseRequest(
    Guid ExpenseAccountId,
    Guid PaymentAccountId,
    string? Description,
    decimal Amount,
    string? Currency);

internal sealed record CreateExpenseResponse(Guid JournalEntryId);
