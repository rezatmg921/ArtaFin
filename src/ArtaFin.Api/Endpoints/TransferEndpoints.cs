using ArtaFin.Application.Features.JournalEntries.CreateJournalEntry;
using ArtaFin.Application.Features.Transfers.CreateTransfer;
using ArtaFin.Domain.Finance;
using MediatR;

namespace ArtaFin.Api.Endpoints;

internal static class TransferEndpoints
{
    public static IEndpointRouteBuilder MapTransferEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/financial-books/{financialBookId:guid}/transfers", CreateAsync)
            .WithName("CreateTransfer")
            .WithTags("Transfers")
            .RequireAuthorization()
            .Produces<CreateTransferResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        Guid financialBookId,
        CreateTransferRequest request,
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
        if (request.SourceAccountId == Guid.Empty)
            errors["sourceAccountId"] = ["Source account id is required."];
        if (request.DestinationAccountId == Guid.Empty)
            errors["destinationAccountId"] = ["Destination account id is required."];
        if (request.SourceAccountId != Guid.Empty && request.SourceAccountId == request.DestinationAccountId)
            errors["destinationAccountId"] = ["Source and destination accounts must be different."];
        if (request.Amount <= 0)
            errors["amount"] = ["Amount must be greater than zero."];

        Money amount;
        try { amount = new Money(request.Amount, request.Currency ?? string.Empty); }
        catch (ArgumentException exception) { errors["currency"] = [exception.Message]; amount = default; }
        catch (NotSupportedException exception) { errors["currency"] = [exception.Message]; amount = default; }

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var entryId = await sender.Send(new CreateTransferCommand(
            financialBookId, request.SourceAccountId, request.DestinationAccountId,
            request.Description!, amount, idempotencyKey), cancellationToken);

        return Results.Created(
            $"/api/financial-books/{financialBookId}/journal-entries/{entryId}",
            new CreateTransferResponse(entryId));
    }
}

internal sealed record CreateTransferRequest(
    Guid SourceAccountId, Guid DestinationAccountId, string? Description, decimal Amount, string? Currency);
internal sealed record CreateTransferResponse(Guid JournalEntryId);
