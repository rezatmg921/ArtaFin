using ArtaFin.Application.Features.FinancialBooks.InviteMember;
using ArtaFin.Domain.Finance;

namespace ArtaFin.Api.Endpoints;

internal static class FinancialBookMemberEndpoints
{
    public static IEndpointRouteBuilder MapFinancialBookMemberEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
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

        return endpoints;
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
}

internal sealed record InviteFinancialBookMemberRequest(string? Email, string? Role);
internal sealed record InviteFinancialBookMemberResponse(Guid Id);
