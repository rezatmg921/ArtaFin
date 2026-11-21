using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ArtaFin.Application.Abstractions.Security;
using Microsoft.IdentityModel.Tokens;

namespace ArtaFin.Api.Security;

internal sealed class JwtTokenService(JwtOptions options) : IAccessTokenGenerator
{
    public (string Token, DateTime ExpiresAtUtc) CreateToken(
        Guid userId,
        string email,
        IReadOnlyCollection<string> roles)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(options.AccessTokenMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        }.Concat(roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(role => new Claim("role", role)))
         .ToArray();

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
