using System.Security.Cryptography;
using System.Text;
using ArtaFin.Application.Abstractions.Security;

namespace ArtaFin.Infrastructure.Security;

/// <summary>Generates opaque 256-bit refresh tokens and stores only SHA-256 hashes.</summary>
public sealed class RefreshTokenService : IRefreshTokenService
{
    public string GenerateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    public string ComputeHash(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }
}
