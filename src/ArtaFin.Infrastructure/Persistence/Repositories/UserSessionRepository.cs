using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Infrastructure.Persistence.Repositories;

internal sealed class UserSessionRepository(ArtaFinDbContext dbContext)
    : IUserSessionRepository
{
    public Task<UserSession?> GetByRefreshTokenHashAsync(
        string refreshTokenHash,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshTokenHash);

        return dbContext.UserSessions.SingleOrDefaultAsync(
            session => session.RefreshTokenHash == refreshTokenHash,
            cancellationToken);
    }

    public async Task AddAsync(
        UserSession session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        await dbContext.UserSessions.AddAsync(session, cancellationToken);
    }

    public async Task RevokeAllForUserAsync(
        Guid userId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken)
    {
        var sessions = await dbContext.UserSessions
            .Where(session => session.UserId == userId && session.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
            session.Revoke(revokedAt);
    }
}
