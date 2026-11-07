using ArtaFin.Domain.Common;

namespace ArtaFin.Domain.Security;

/// <summary>
/// Represents one authenticated device/session. Only a hash of the refresh
/// token is persisted; the raw token is returned to the client once.
/// </summary>
public sealed class UserSession : Entity
{
    public Guid UserId { get; private set; }
    public string RefreshTokenHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? ReplacedBySessionId { get; private set; }

    private UserSession(
        Guid id,
        Guid userId,
        string refreshTokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? revokedAt,
        Guid? replacedBySessionId) : base(id)
    {
        UserId = userId;
        RefreshTokenHash = refreshTokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        RevokedAt = revokedAt;
        ReplacedBySessionId = replacedBySessionId;
    }

    public static UserSession Start(
        Guid id,
        Guid userId,
        string refreshTokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Session id cannot be empty.", nameof(id));
        if (userId == Guid.Empty)
            throw new ArgumentException("User id cannot be empty.", nameof(userId));
        if (string.IsNullOrWhiteSpace(refreshTokenHash))
            throw new ArgumentException("Refresh token hash is required.", nameof(refreshTokenHash));
        if (expiresAt <= createdAt)
            throw new ArgumentException("Session expiry must be after creation.", nameof(expiresAt));

        return new UserSession(id, userId, refreshTokenHash, createdAt, expiresAt, null, null);
    }

    public void Revoke(DateTimeOffset revokedAt, Guid? replacedBySessionId = null)
    {
        if (RevokedAt is not null)
            return;
        if (replacedBySessionId == Id)
            throw new ArgumentException("A session cannot replace itself.", nameof(replacedBySessionId));

        RevokedAt = revokedAt;
        ReplacedBySessionId = replacedBySessionId;
    }

    public bool IsUsableAt(DateTimeOffset now) =>
        RevokedAt is null && ExpiresAt > now;
}
