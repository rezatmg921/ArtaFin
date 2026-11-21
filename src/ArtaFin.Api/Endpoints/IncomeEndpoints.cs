using ArtaFin.Application.Features.Income.CreateIncome;
using ArtaFin.Application.Features.JournalEntries.CreateJournalEntry;
using ArtaFin.Domain.Finance;
using MediatR;

namespace ArtaFin.Api.Endpoints;

internal static class IncomeEndpoints
{
    public static IEndpointRouteBuilder MapIncomeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/financial-books/{financialBookId:guid}/income", CreateAsync)
            .WithName("CreateIncome")
            .WithTags("Income")
            .RequireAuthorization("Permission:Transactions.Create")
            .Produces<CreateIncomeResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        Guid financialBookId,
        CreateIncomeRequest request,
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
        if (request.IncomeAccountId == Guid.Empty)
            errors["incomeAccountId"] = ["Income account id is required."];
        if (request.DepositAccountId == Guid.Empty)
            errors["depositAccountId"] = ["Deposit account id is required."];
        if (request.IncomeAccountId != Guid.Empty && request.IncomeAccountId == request.DepositAccountId)
            errors["depositAccountId"] = ["Income and deposit accounts must be different."];
        if (request.Amount <= 0)
            errors["amount"] = ["Amount must be greater than zero."];

        Money amount;
        try { amount = new Money(request.Amount, request.Currency ?? string.Empty); }
        catch (ArgumentException exception) { errors["currency"] = [exception.Message]; amount = default; }
        catch (NotSupportedException exception) { errors["currency"] = [exception.Message]; amount = default; }

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var entryId = await sender.Send(new CreateIncomeCommand(
            financialBookId, request.IncomeAccountId, request.DepositAccountId, request.Description!, amount, idempotencyKey), cancellationToken);
        return Results.Created(
            $"/api/financial-books/{financialBookId}/journal-entries/{entryId}",
            new CreateIncomeResponse(entryId));
    }
}

internal sealed record CreateIncomeRequest(
    Guid IncomeAccountId, Guid DepositAccountId, string? Description, decimal Amount, string? Currency);
internal sealed record CreateIncomeResponse(Guid JournalEntryId);
