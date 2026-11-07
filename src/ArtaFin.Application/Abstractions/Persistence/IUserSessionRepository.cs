using ArtaFin.Domain.Security;

namespace ArtaFin.Application.Abstractions.Persistence;

/// <summary>Persistence port for refresh-token-backed user sessions.</summary>
public interface IUserSessionRepository
{
    Task<UserSession?> GetByRefreshTokenHashAsync(
        string refreshTokenHash,
        CancellationToken cancellationToken);

    Task AddAsync(UserSession session, CancellationToken cancellationToken);

    Task RevokeAllForUserAsync(
        Guid userId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken);
}
