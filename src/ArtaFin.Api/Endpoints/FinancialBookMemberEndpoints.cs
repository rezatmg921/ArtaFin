using ArtaFin.Application.Features.FinancialBooks.InviteMember;
using ArtaFin.Application.Features.FinancialBooks.ListMembers;
using ArtaFin.Application.Features.FinancialBooks.ManageMember;
using ArtaFin.Domain.Finance;

namespace ArtaFin.Api.Endpoints;

internal static class FinancialBookMemberEndpoints
{
    public static IEndpointRouteBuilder MapFinancialBookMemberEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/financial-books/{financialBookId:guid}/members",
                ListAsync)
            .WithName("ListFinancialBookMembers")
            .WithTags("FinancialBookMembers")
            .RequireAuthorization()
            .Produces<IReadOnlyList<FinancialBookMemberResponse>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        endpoints.MapPost(
                "/api/financial-books/{financialBookId:guid}/members",
                InviteAsync)
            .WithName("InviteFinancialBookMember")
            .WithTags("FinancialBookMembers")
            .RequireAuthorization()
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        endpoints.MapPut(
                "/api/financial-books/{financialBookId:guid}/members/{membershipId:guid}/role",
                ChangeRoleAsync)
            .WithName("ChangeFinancialBookMemberRole")
            .WithTags("FinancialBookMembers")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        endpoints.MapDelete(
                "/api/financial-books/{financialBookId:guid}/members/{membershipId:guid}",
                RevokeAsync)
            .WithName("RevokeFinancialBookMember")
            .WithTags("FinancialBookMembers")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid financialBookId,
        ListFinancialBookMembersQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var members = await handler.HandleAsync(
            new ListFinancialBookMembersQuery(financialBookId),
            cancellationToken);

        var response = members
            .Select(member => new FinancialBookMemberResponse(
                member.MembershipId,
                member.UserId,
                member.Email,
                member.Role.ToString(),
                member.GrantedAt))
            .ToArray();

        return Results.Ok(response);
    }

    private static async Task<IResult> InviteAsync(
        Guid financialBookId,
        InviteFinancialBookMemberRequest request,
        InviteFinancialBookMemberCommandHandler handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["email"] = ["Email is required."]
            });

        if (!Enum.TryParse<FinancialBookRole>(request.Role, true, out var role) ||
            !Enum.IsDefined(role))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["role"] = ["Role must be Accountant, Auditor, or Viewer."]
            });
        }

        var memberId = await handler.HandleAsync(
            new InviteFinancialBookMemberCommand(financialBookId, request.Email, role),
            cancellationToken);

        return Results.Created(
            $"/api/financial-books/{financialBookId}/members/{memberId}",
            new InviteFinancialBookMemberResponse(memberId));
    }

    private static async Task<IResult> ChangeRoleAsync(
        Guid financialBookId,
        Guid membershipId,
        ChangeFinancialBookMemberRoleRequest request,
        ChangeFinancialBookMemberRoleCommandHandler handler,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<FinancialBookRole>(request.Role, true, out var role) ||
            !Enum.IsDefined(role))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["role"] = ["Role must be Accountant, Auditor, or Viewer."]
            });
        }

        await handler.HandleAsync(
            new ChangeFinancialBookMemberRoleCommand(financialBookId, membershipId, role),
            cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> RevokeAsync(
        Guid financialBookId,
        Guid membershipId,
        RevokeFinancialBookMemberCommandHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(
            new RevokeFinancialBookMemberCommand(financialBookId, membershipId),
            cancellationToken);

        return Results.NoContent();
    }
}

internal sealed record FinancialBookMemberResponse(
    Guid MembershipId,
    Guid UserId,
    string Email,
    string Role,
    DateTimeOffset GrantedAt);

internal sealed record InviteFinancialBookMemberRequest(string? Email, string? Role);
internal sealed record InviteFinancialBookMemberResponse(Guid Id);
internal sealed record ChangeFinancialBookMemberRoleRequest(string? Role);
