namespace ArtaFin.Application.Abstractions.Security;

public interface IAccessTokenGenerator
{
    (string Token, DateTime ExpiresAtUtc) CreateToken(
        Guid userId,
        string email,
        IReadOnlyCollection<string> roles);

    // Compatibility overload for existing test helpers and callers that do not
    // need to issue role-bearing tokens explicitly.
    (string Token, DateTime ExpiresAtUtc) CreateToken(Guid userId, string email) =>
        CreateToken(userId, email, Array.Empty<string>());
}
