using ArtaFin.Domain.Common;

namespace ArtaFin.Domain.Security;

/// <summary>
/// Stores a self-describing password hash and credential security state.
/// Plaintext passwords must never be passed to or stored by this entity.
/// </summary>
public sealed class UserCredential : Entity
{
    public Guid UserId { get; private set; }
    public string PasswordHash { get; private set; } = string.Empty;
    public string HashAlgorithm { get; private set; } = string.Empty;
    public DateTimeOffset PasswordChangedAt { get; private set; }
    public int FailedLoginCount { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }

    private UserCredential(
        Guid id,
        Guid userId,
        string passwordHash,
        string hashAlgorithm,
        DateTimeOffset passwordChangedAt,
        int failedLoginCount,
        DateTimeOffset? lockedUntil) : base(id)
    {
        UserId = userId;
        PasswordHash = passwordHash;
        HashAlgorithm = hashAlgorithm;
        PasswordChangedAt = passwordChangedAt;
        FailedLoginCount = failedLoginCount;
        LockedUntil = lockedUntil;
    }

    public static UserCredential Create(
        Guid id,
        Guid userId,
        string passwordHash,
        string hashAlgorithm,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Credential id cannot be empty.", nameof(id));
        if (userId == Guid.Empty)
            throw new ArgumentException("User id cannot be empty.", nameof(userId));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        if (string.IsNullOrWhiteSpace(hashAlgorithm))
            throw new ArgumentException("Hash algorithm identifier is required.", nameof(hashAlgorithm));

        return new UserCredential(
            id, userId, passwordHash, hashAlgorithm, createdAt, 0, null);
    }

    public void ReplacePassword(
        string passwordHash,
        string hashAlgorithm,
        DateTimeOffset changedAt)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        if (string.IsNullOrWhiteSpace(hashAlgorithm))
            throw new ArgumentException("Hash algorithm identifier is required.", nameof(hashAlgorithm));

        PasswordHash = passwordHash;
        HashAlgorithm = hashAlgorithm;
        PasswordChangedAt = changedAt;
        ResetFailedLogins();
    }

    public void RecordFailedLogin(int maximumAttempts, DateTimeOffset now, TimeSpan lockoutDuration)
    {
        if (maximumAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumAttempts));
        if (lockoutDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(lockoutDuration));

        if (LockedUntil is not null && LockedUntil > now)
            return;

        FailedLoginCount++;

        if (FailedLoginCount >= maximumAttempts)
        {
            LockedUntil = now.Add(lockoutDuration);
            FailedLoginCount = 0;
        }
    }

    public void ResetFailedLogins()
    {
        FailedLoginCount = 0;
        LockedUntil = null;
    }

    public bool IsLockedAt(DateTimeOffset now) => LockedUntil is not null && LockedUntil > now;
}
