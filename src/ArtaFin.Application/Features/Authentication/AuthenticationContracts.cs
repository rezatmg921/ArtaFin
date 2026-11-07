namespace ArtaFin.Application.Features.Authentication;

public sealed record RegisterCommand(string? Email, string? Password);
public sealed record LoginCommand(string? Email, string? Password);
public sealed record RefreshCommand(string? RefreshToken);
public sealed record LogoutCommand(string? RefreshToken);

public sealed record AuthenticationResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc)
{
    public string TokenType => "Bearer";
}

public enum AuthenticationFailure
{
    None = 0,
    Validation = 1,
    InvalidCredentials = 2,
    AccountDisabled = 3,
    AccountLocked = 4,
    EmailAlreadyRegistered = 5,
    InvalidRefreshToken = 6
}

public sealed record AuthenticationResult(
    AuthenticationResponse? Response,
    AuthenticationFailure Failure,
    IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public bool Succeeded => Response is not null;

    public static AuthenticationResult Success(AuthenticationResponse response) =>
        new(response, AuthenticationFailure.None);

    public static AuthenticationResult Failed(
        AuthenticationFailure failure,
        IReadOnlyDictionary<string, string[]>? errors = null) =>
        new(null, failure, errors);
}
