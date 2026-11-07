using ArtaFin.Api.Security;
using Microsoft.AspNetCore.Identity;

namespace ArtaFin.Api.Endpoints;

internal static class AuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth")
            .WithTags("Authentication")
            .AllowAnonymous();

        group.MapPost("/register", RegisterAsync)
            .WithName("Register")
            .Produces<TokenResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPost("/login", LoginAsync)
            .WithName("Login")
            .Produces<TokenResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        UserManager<IdentityUser<Guid>> userManager,
        JwtTokenService tokenService)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["credentials"] = ["Email and password are required."]
            });
        }

        var email = request.Email.Trim();
        var user = new IdentityUser<Guid>
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors
                .GroupBy(error => error.Code)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.Description).ToArray());

            return Results.ValidationProblem(errors);
        }

        var (token, expiresAtUtc) = tokenService.CreateToken(user.Id, email);
        return Results.Created(
            "/api/auth/me",
            new TokenResponse(token, expiresAtUtc));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<IdentityUser<Guid>> userManager,
        SignInManager<IdentityUser<Guid>> signInManager,
        JwtTokenService tokenService)
    {
        var user = await userManager.FindByEmailAsync(request.Email?.Trim() ?? string.Empty);

        if (user is null)
            return Results.Unauthorized();

        var result = await signInManager.CheckPasswordSignInAsync(
            user,
            request.Password ?? string.Empty,
            lockoutOnFailure: true);

        if (!result.Succeeded || user.Email is null)
            return Results.Unauthorized();

        var (token, expiresAtUtc) = tokenService.CreateToken(user.Id, user.Email);
        return Results.Ok(new TokenResponse(token, expiresAtUtc));
    }
}

internal sealed record RegisterRequest(string? Email, string? Password);
internal sealed record LoginRequest(string? Email, string? Password);
internal sealed record TokenResponse(string AccessToken, DateTime ExpiresAtUtc)
{
    public string TokenType => "Bearer";
}
