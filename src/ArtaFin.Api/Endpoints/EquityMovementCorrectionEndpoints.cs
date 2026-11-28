using ArtaFin.Application.Features.EquityMovementCorrections;
using ArtaFin.Domain.Finance;

namespace ArtaFin.Api.Endpoints;

internal static class EquityMovementCorrectionEndpoints
{
    public static IEndpointRouteBuilder MapEquityMovementCorrectionEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/api/financial-books/{financialBookId:guid}/journal-entries/{journalEntryId:guid}/equity-movement-corrections",
                CreateAsync)
            .WithName("CreateEquityMovementCorrections")
            .WithTags("EquityMovementCorrections")
            .RequireAuthorization("Permission:JournalEntries.CorrectClassification")
            .Produces<IReadOnlyList<EquityMovementCorrectionResult>>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        endpoints.MapGet(
                "/api/financial-books/{financialBookId:guid}/journal-entries/{journalEntryId:guid}/equity-movement-corrections",
                GetByJournalEntryAsync)
            .WithName("GetEquityMovementCorrections")
            .WithTags("EquityMovementCorrections")
            .RequireAuthorization("Permission:Transactions.Read")
            .Produces<IReadOnlyList<EquityMovementCorrectionResult>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        Guid financialBookId,
        Guid journalEntryId,
        CreateEquityMovementCorrectionsRequest request,
        EquityMovementCorrectionService service,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Items is null || request.Items.Count == 0)
            errors["items"] = ["At least one correction item is required."];

        var items = new List<CreateEquityMovementCorrectionItem>();
        if (request.Items is not null)
        {
            for (var index = 0; index < request.Items.Count; index++)
            {
                var item = request.Items[index];
                if (item.LineNumber <= 0)
                    errors[$"items[{index}].lineNumber"] = ["Line number must be greater than zero."];

                if (!Enum.TryParse<EquityMovementType>(item.ExpectedOriginalMovementType, true, out var original) ||
                    !Enum.IsDefined(original))
                    errors[$"items[{index}].expectedOriginalMovementType"] = ["Unknown equity movement type."];

                if (!Enum.TryParse<EquityMovementType>(item.CorrectedMovementType, true, out var corrected) ||
                    !Enum.IsDefined(corrected) || corrected == EquityMovementType.Unclassified)
                    errors[$"items[{index}].correctedMovementType"] = ["Corrected type must be OwnerContribution, OwnerDistribution, TransferBetweenEquityAccounts, or Other."];

                if (string.IsNullOrWhiteSpace(item.Reason) ||
                    item.Reason.Trim().Length > EquityMovementCorrection.MaxReasonLength)
                    errors[$"items[{index}].reason"] = [$"Reason is required and cannot exceed {EquityMovementCorrection.MaxReasonLength} characters."];

                if (Enum.TryParse<EquityMovementType>(item.ExpectedOriginalMovementType, true, out original) &&
                    Enum.IsDefined(original) &&
                    Enum.TryParse<EquityMovementType>(item.CorrectedMovementType, true, out corrected) &&
                    Enum.IsDefined(corrected) && corrected != EquityMovementType.Unclassified)
                {
                    items.Add(new CreateEquityMovementCorrectionItem(
                        item.LineNumber, original, corrected, item.Reason ?? string.Empty));
                }
            }
        }

        if (request.Items is not null &&
            request.Items.Select(item => item.LineNumber).Distinct().Count() != request.Items.Count)
            errors["items"] = ["Each line number may appear only once."];

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var result = await service.CreateAsync(
            financialBookId,
            journalEntryId,
            new CreateEquityMovementCorrectionsInput(items),
            cancellationToken);

        var location = $"/api/financial-books/{financialBookId}/journal-entries/{journalEntryId}/equity-movement-corrections";
        return Results.Created(location, result);
    }

    private static async Task<IResult> GetByJournalEntryAsync(
        Guid financialBookId,
        Guid journalEntryId,
        EquityMovementCorrectionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetByJournalEntryAsync(
            financialBookId, journalEntryId, cancellationToken);
        return Results.Ok(result);
    }
}

internal sealed record CreateEquityMovementCorrectionsRequest(
    IReadOnlyList<CreateEquityMovementCorrectionRequestItem>? Items);

internal sealed record CreateEquityMovementCorrectionRequestItem(
    int LineNumber,
    string? ExpectedOriginalMovementType,
    string? CorrectedMovementType,
    string? Reason);
