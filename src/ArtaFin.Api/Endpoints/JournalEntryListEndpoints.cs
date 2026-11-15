using ArtaFin.Application.Features.JournalEntries.ListJournalEntries;
using MediatR;

namespace ArtaFin.Api.Endpoints;

internal static class JournalEntryListEndpoints
{
    public static IEndpointRouteBuilder MapJournalEntryListEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/financial-books/{financialBookId:guid}/journal-entries",
                ListAsync)
            .WithName("ListJournalEntries")
            .WithTags("JournalEntries")
            .RequireAuthorization()
            .Produces<JournalEntryListResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid financialBookId,
        ISender sender,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 50,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? search = null,
        bool? isReversal = null)
    {
        var errors = new Dictionary<string, string[]>();

        if (page < 1)
            errors["page"] = ["Page must be greater than zero."];
        if (pageSize is < 1 or > 100)
            errors["pageSize"] = ["Page size must be between 1 and 100."];
        if (page > 0 && pageSize > 0 && page > int.MaxValue / pageSize)
            errors["page"] = ["Requested page is too large."];
        if (search is not null && search.Trim().Length > 200)
            errors["search"] = ["Search term cannot exceed 200 characters."];
        if (from.HasValue && to.HasValue && from > to)
            errors["to"] = ["To date must be greater than or equal to From date."];

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var result = await sender.Send(
            new ListJournalEntriesQuery(
                financialBookId,
                from,
                to,
                search,
                isReversal,
                page,
                pageSize),
            cancellationToken);

        return Results.Ok(new JournalEntryListResponse(
            page,
            pageSize,
            result.TotalCount,
            result.Items.Select(entry => new JournalEntryListItemResponse(
                entry.Id,
                entry.PostedAt,
                entry.Description,
                entry.ReversesJournalEntryId,
                entry.LineCount)).ToArray()));
    }
}

internal sealed record JournalEntryListResponse(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<JournalEntryListItemResponse> Items);

internal sealed record JournalEntryListItemResponse(
    Guid Id,
    DateTimeOffset PostedAt,
    string Description,
    Guid? ReversesJournalEntryId,
    int LineCount);
