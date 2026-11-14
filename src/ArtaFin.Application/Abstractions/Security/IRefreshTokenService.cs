namespace ArtaFin.Application.Abstractions.Security;

public interface IRefreshTokenService
{
    string GenerateToken();
    string ComputeHash(string token);
}
