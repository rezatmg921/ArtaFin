using System.Security.Claims;
using ArtaFin.Application.Features.Authentication;

namespace ArtaFin.Api.Endpoints;

internal static class AuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth")
            .WithTags("Authentication");

        group.MapPost("/register", RegisterAsync)
            .AllowAnonymous()
            .WithName("Register")
            .Produces<AuthenticationResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .WithName("Login")
            .Produces<AuthenticationResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/refresh", RefreshAsync)
            .AllowAnonymous()
            .WithName("RefreshTokens")
            .Produces<AuthenticationResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", LogoutAsync)
            .AllowAnonymous()
            .WithName("Logout")
            .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/me", (ClaimsPrincipal principal) =>
            Results.Ok(new
            {
                UserId = principal.FindFirst("sub")?.Value,
                Email = principal.FindFirst("email")?.Value
            }))
            .RequireAuthorization()
            .WithName("GetCurrentUser");

        return endpoints;
    }

    private static async Task<IResult> RefreshAsync(
        RefreshRequest request,
        AuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.RefreshAsync(
            new RefreshCommand(request.RefreshToken),
            cancellationToken);

        return result.Succeeded
            ? Results.Ok(result.Response)
            : Results.Unauthorized();
    }

    private static async Task<IResult> LogoutAsync(
        LogoutRequest request,
        AuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        await authenticationService.LogoutAsync(
            new LogoutCommand(request.RefreshToken),
            cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        AuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.RegisterAsync(
            new RegisterCommand(request.Email, request.Password),
            cancellationToken);

        if (result.Succeeded)
        {
            return Results.Created(
                "/api/auth/me",
                result.Response);
        }

        if (result.Failure == AuthenticationFailure.EmailAlreadyRegistered)
            return Results.Conflict(result.Errors);

        return Results.ValidationProblem(
            result.Errors?.ToDictionary(pair => pair.Key, pair => pair.Value)
            ?? new Dictionary<string, string[]>
            {
                ["credentials"] = ["Registration details are invalid."]
            });
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        AuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.LoginAsync(
            new LoginCommand(request.Email, request.Password),
            cancellationToken);

        return result.Succeeded
            ? Results.Ok(result.Response)
            : Results.Unauthorized();
    }
}

internal sealed record RegisterRequest(string? Email, string? Password);
internal sealed record LoginRequest(string? Email, string? Password);
internal sealed record RefreshRequest(string? RefreshToken);
internal sealed record LogoutRequest(string? RefreshToken);
