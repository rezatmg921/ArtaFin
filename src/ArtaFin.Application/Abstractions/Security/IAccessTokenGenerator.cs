namespace ArtaFin.Application.Abstractions.Security;

public interface IAccessTokenGenerator
{
    (string Token, DateTime ExpiresAtUtc) CreateToken(Guid userId, string email);
}
