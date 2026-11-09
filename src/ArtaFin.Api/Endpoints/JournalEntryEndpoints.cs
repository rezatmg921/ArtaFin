using ArtaFin.Application.Features.JournalEntries.CreateJournalEntry;
using ArtaFin.Domain.Finance;

namespace ArtaFin.Api.Endpoints;

internal static class JournalEntryEndpoints
{
    private const int MaxLines = 100;

    public static IEndpointRouteBuilder MapJournalEntryEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/api/financial-books/{financialBookId:guid}/journal-entries",
                CreateAsync)
            .WithName("CreateJournalEntry")
            .WithTags("JournalEntries")
            .RequireAuthorization()
            .Produces<CreateJournalEntryResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        Guid financialBookId,
        CreateJournalEntryRequest request,
        CreateJournalEntryCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Description))
            errors["description"] = ["Description is required."];
        else if (request.Description.Trim().Length > JournalEntry.MaxDescriptionLength)
            errors["description"] = [$"Description cannot exceed {JournalEntry.MaxDescriptionLength} characters."];

        if (request.Lines is null || request.Lines.Count < 2)
        {
            errors["lines"] = ["At least two journal lines are required."];
        }
        else if (request.Lines.Count > MaxLines)
        {
            errors["lines"] = [$"A journal entry cannot contain more than {MaxLines} lines."];
        }

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var lines = new List<JournalEntryLineCommand>(request.Lines!.Count);

        for (var index = 0; index < request.Lines.Count; index++)
        {
            var line = request.Lines.ElementAt(index);
            var prefix = $"lines[{index}]";

            if (line is null)
            {
                errors[prefix] = ["Journal line is required."];
                continue;
            }

            if (line.AccountId == Guid.Empty)
                errors[$"{prefix}.accountId"] = ["Account id is required."];

            if (!Enum.TryParse<PostingSide>(line.Side, true, out var side) ||
                !Enum.IsDefined(side))
            {
                errors[$"{prefix}.side"] = ["Side must be Debit or Credit."];
            }

            if (line.Amount <= 0)
                errors[$"{prefix}.amount"] = ["Amount must be greater than zero."];

            Money amount;
            try
            {
                amount = new Money(line.Amount, line.Currency ?? string.Empty);
            }
            catch (ArgumentException exception)
            {
                errors[$"{prefix}.currency"] = [exception.Message];
                continue;
            }
            catch (NotSupportedException exception)
            {
                errors[$"{prefix}.currency"] = [exception.Message];
                continue;
            }

            if (line.AccountId != Guid.Empty &&
                Enum.TryParse<PostingSide>(line.Side, true, out side) &&
                Enum.IsDefined(side) &&
                line.Amount > 0)
            {
                lines.Add(new JournalEntryLineCommand(line.AccountId, side, amount));
            }
        }

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var entryId = await handler.HandleAsync(
            new CreateJournalEntryCommand(financialBookId, request.Description!, lines),
            cancellationToken);

        var location = $"/api/financial-books/{financialBookId}/journal-entries/{entryId}";
        return Results.Created(location, new CreateJournalEntryResponse(entryId));
    }
}

internal sealed record CreateJournalEntryRequest(
    string? Description,
    IReadOnlyList<CreateJournalEntryLineRequest?>? Lines);

internal sealed record CreateJournalEntryLineRequest(
    Guid AccountId,
    string? Side,
    decimal Amount,
    string? Currency);

internal sealed record CreateJournalEntryResponse(Guid Id);
