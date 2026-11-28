using ArtaFin.Application.Features.Contacts;
using ArtaFin.Domain.Contacts;

namespace ArtaFin.Api.Endpoints;

internal static class ContactEndpoints
{
    public static IEndpointRouteBuilder MapContactEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/financial-books/{financialBookId:guid}/contacts", ListAsync)
            .WithName("ListContacts").WithTags("Contacts")
            .RequireAuthorization("Permission:Contacts.Read")
            .Produces<IReadOnlyList<ContactResponse>>()
            .Produces(StatusCodes.Status401Unauthorized).Produces(StatusCodes.Status403Forbidden);

        endpoints.MapGet("/api/financial-books/{financialBookId:guid}/contacts/{contactId:guid}", GetAsync)
            .WithName("GetContact").WithTags("Contacts")
            .RequireAuthorization("Permission:Contacts.Read")
            .Produces<ContactResponse>().Produces(StatusCodes.Status404NotFound);

        endpoints.MapPost("/api/financial-books/{financialBookId:guid}/contacts", CreateAsync)
            .WithName("CreateContact").WithTags("Contacts")
            .RequireAuthorization("Permission:Contacts.Create")
            .Produces<CreateContactResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem().Produces(StatusCodes.Status403Forbidden);

        endpoints.MapPut("/api/financial-books/{financialBookId:guid}/contacts/{contactId:guid}", UpdateAsync)
            .WithName("UpdateContact").WithTags("Contacts")
            .RequireAuthorization("Permission:Contacts.Update")
            .Produces(StatusCodes.Status204NoContent).ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        endpoints.MapPost("/api/financial-books/{financialBookId:guid}/contacts/{contactId:guid}/archive", ArchiveAsync)
            .WithName("ArchiveContact").WithTags("Contacts")
            .RequireAuthorization("Permission:Contacts.Archive")
            .Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid financialBookId, bool? includeArchived, int? skip, int? take,
        ContactService service, CancellationToken cancellationToken)
    {
        var pageSkip = skip ?? 0;
        var pageTake = take ?? 50;
        if (pageSkip < 0 || pageTake is < 1 or > 100)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["pagination"] = ["Skip must be zero or greater and take must be between 1 and 100."]
            });

        var contacts = await service.ListAsync(financialBookId, includeArchived ?? false,
            pageSkip, pageTake, cancellationToken);
        return Results.Ok(contacts.Select(ToResponse).ToArray());
    }

    private static async Task<IResult> GetAsync(
        Guid financialBookId, Guid contactId, ContactService service,
        CancellationToken cancellationToken)
    {
        var contact = await service.GetByIdAsync(financialBookId, contactId, cancellationToken);
        return contact is null ? Results.NotFound() : Results.Ok(ToResponse(contact));
    }

    private static async Task<IResult> CreateAsync(
        Guid financialBookId, CreateContactRequest request, ContactService service,
        CancellationToken cancellationToken)
    {
        var errors = Validate(request.Type, request.DisplayName);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var id = await service.CreateAsync(financialBookId, ParseType(request.Type),
            request.DisplayName!, request.Email, request.Phone, request.Notes, cancellationToken);
        return Results.Created($"/api/financial-books/{financialBookId}/contacts/{id}",
            new CreateContactResponse(id));
    }

    private static async Task<IResult> UpdateAsync(
        Guid financialBookId, Guid contactId, UpdateContactRequest request,
        ContactService service, CancellationToken cancellationToken)
    {
        var errors = Validate(request.Type, request.DisplayName);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        await service.UpdateAsync(financialBookId, contactId, ParseType(request.Type),
            request.DisplayName!, request.Email, request.Phone, request.Notes, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ArchiveAsync(
        Guid financialBookId, Guid contactId, ContactService service,
        CancellationToken cancellationToken)
    {
        await service.ArchiveAsync(financialBookId, contactId, cancellationToken);
        return Results.NoContent();
    }

    private static Dictionary<string, string[]> Validate(string? type, string? displayName)
    {
        var errors = new Dictionary<string, string[]>();
        if (!Enum.TryParse<ContactType>(type, true, out var parsed) || !Enum.IsDefined(parsed))
            errors["type"] = ["Type must be Person or Organization."];
        if (string.IsNullOrWhiteSpace(displayName))
            errors["displayName"] = ["Display name is required."];
        else if (displayName.Trim().Length > Contact.MaxDisplayNameLength)
            errors["displayName"] = [$"Display name cannot exceed {Contact.MaxDisplayNameLength} characters."];
        return errors;
    }

    private static ContactType ParseType(string? value) =>
        Enum.Parse<ContactType>(value!, true);

    private static ContactResponse ToResponse(ContactSnapshot contact) => new(
        contact.Id, contact.FinancialBookId, contact.Type.ToString(),
        contact.DisplayName, contact.Email, contact.Phone, contact.Notes,
        contact.CreatedAt, contact.UpdatedAt, contact.ArchivedAt);

    private sealed record ContactResponse(
        Guid Id, Guid FinancialBookId, string Type, string DisplayName,
        string? Email, string? Phone, string? Notes, DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt, DateTimeOffset? ArchivedAt);
    private sealed record CreateContactResponse(Guid Id);
    private sealed record CreateContactRequest(
        string? Type, string? DisplayName, string? Email, string? Phone, string? Notes);
    private sealed record UpdateContactRequest(
        string? Type, string? DisplayName, string? Email, string? Phone, string? Notes);
}
