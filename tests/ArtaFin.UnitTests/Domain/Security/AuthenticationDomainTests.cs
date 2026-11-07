using ArtaFin.Domain.Security;
using Xunit;

namespace ArtaFin.UnitTests.Domain.Security;

public sealed class AuthenticationDomainTests
{
    [Fact]
    public void Register_NormalizesEmail_AndActivatesUser()
    {
        var now = DateTimeOffset.UtcNow;

        var user = AppUser.Register(
            Guid.NewGuid(),
            "  Alice@example.com ",
            now);

        Assert.Equal("Alice@example.com", user.Email);
        Assert.Equal("ALICE@EXAMPLE.COM", user.NormalizedEmail);
        Assert.True(user.IsActive);
        Assert.Null(user.DisabledAt);
    }

    [Fact]
    public void Disable_AndReactivate_ChangeAccountState()
    {
        var now = DateTimeOffset.UtcNow;
        var user = AppUser.Register(Guid.NewGuid(), "alice@example.com", now);

        user.Disable(now.AddMinutes(1));

        Assert.False(user.IsActive);
        Assert.Equal(now.AddMinutes(1), user.DisabledAt);

        user.Reactivate();

        Assert.True(user.IsActive);
        Assert.Null(user.DisabledAt);
    }

    [Fact]
    public void RecordFailedLogin_LocksCredentialAtConfiguredThreshold()
    {
        var now = DateTimeOffset.UtcNow;
        var credential = UserCredential.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "encoded-password-hash",
            "PBKDF2-SHA256",
            now);

        credential.RecordFailedLogin(3, now, TimeSpan.FromMinutes(15));
        credential.RecordFailedLogin(3, now, TimeSpan.FromMinutes(15));

        Assert.False(credential.IsLockedAt(now));
        Assert.Equal(2, credential.FailedLoginCount);

        credential.RecordFailedLogin(3, now, TimeSpan.FromMinutes(15));

        Assert.True(credential.IsLockedAt(now));
        Assert.Equal(0, credential.FailedLoginCount);
        Assert.Equal(now.AddMinutes(15), credential.LockedUntil);
    }

    [Fact]
    public void ReplacePassword_ResetsFailedLoginState()
    {
        var now = DateTimeOffset.UtcNow;
        var credential = UserCredential.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "old-hash",
            "PBKDF2-SHA256",
            now);

        credential.RecordFailedLogin(5, now, TimeSpan.FromMinutes(10));
        credential.ReplacePassword("new-hash", "PBKDF2-SHA256", now.AddMinutes(2));

        Assert.Equal("new-hash", credential.PasswordHash);
        Assert.Equal(0, credential.FailedLoginCount);
        Assert.Null(credential.LockedUntil);
        Assert.Equal(now.AddMinutes(2), credential.PasswordChangedAt);
    }

    [Fact]
    public void RevokeSession_MakesSessionUnusable_AndRecordsReplacement()
    {
        var now = DateTimeOffset.UtcNow;
        var replacementId = Guid.NewGuid();
        var session = UserSession.Start(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "hashed-refresh-token",
            now,
            now.AddDays(30));

        Assert.True(session.IsUsableAt(now));

        session.Revoke(now.AddMinutes(5), replacementId);

        Assert.False(session.IsUsableAt(now.AddMinutes(6)));
        Assert.Equal(now.AddMinutes(5), session.RevokedAt);
        Assert.Equal(replacementId, session.ReplacedBySessionId);
    }
}
