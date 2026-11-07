using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Authentication;
using ArtaFin.Domain.Security;

namespace ArtaFin.UnitTests.Application.Authentication;

public sealed class AuthenticationServiceTests
{
    [Fact]
    public async Task RegisterAsync_ShouldCreateUserAndCredential()
    {
        var repository = new FakeUserRepository();
        var hasher = new FakePasswordHasher();
        var service = CreateService(repository, hasher);

        var result = await service.RegisterAsync(
            new RegisterCommand(" person@example.com ", "Valid-Password-42!"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(repository.User);
        Assert.Equal("PERSON@EXAMPLE.COM", repository.User.NormalizedEmail);
        Assert.NotNull(repository.Credential);
        Assert.Equal("fake-hash", repository.Credential.PasswordHash);
        Assert.Equal(1, repository.SaveCount);
        Assert.Single(repository.Sessions);
    }

    [Fact]
    public async Task RegisterAsync_ShouldRejectWeakPassword()
    {
        var repository = new FakeUserRepository();
        var service = CreateService(repository, new FakePasswordHasher());

        var result = await service.RegisterAsync(
            new RegisterCommand("person@example.com", "weak"),
            CancellationToken.None);

        Assert.Equal(AuthenticationFailure.Validation, result.Failure);
        Assert.Null(repository.User);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task LoginAsync_ShouldLockCredentialAfterFiveFailedAttempts()
    {
        var repository = new FakeUserRepository();
        var user = AppUser.Register(
            Guid.NewGuid(),
            "person@example.com",
            DateTimeOffset.UtcNow);
        repository.User = user;
        repository.Credential = UserCredential.Create(
            Guid.NewGuid(),
            user.Id,
            "fake-hash",
            "test",
            DateTimeOffset.UtcNow);

        var service = CreateService(repository, new FakePasswordHasher
        {
            VerificationResult = PasswordVerificationResult.Failed
        });

        AuthenticationResult? result = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            result = await service.LoginAsync(
                new LoginCommand("person@example.com", "Wrong-Password-42!"),
                CancellationToken.None);
        }

        Assert.NotNull(result);
        Assert.Equal(AuthenticationFailure.AccountLocked, result.Failure);
        Assert.True(repository.Credential.IsLockedAt(DateTimeOffset.UtcNow));
        Assert.Equal(5, repository.SaveCount);
    }


    [Fact]
    public async Task RefreshAsync_ShouldRotateTokenAndRevokePreviousSession()
    {
        var repository = new FakeUserRepository();
        var hasher = new FakePasswordHasher();
        var service = CreateService(repository, hasher);
        var registration = await service.RegisterAsync(
            new RegisterCommand("person@example.com", "Valid-Password-42!"),
            CancellationToken.None);

        var oldToken = registration.Response!.RefreshToken;
        var oldSession = Assert.Single(repository.Sessions);

        var refreshed = await service.RefreshAsync(
            new RefreshCommand(oldToken),
            CancellationToken.None);

        Assert.True(refreshed.Succeeded);
        Assert.NotEqual(oldToken, refreshed.Response!.RefreshToken);
        Assert.NotNull(oldSession.RevokedAt);
        Assert.Equal(repository.Sessions[1].Id, oldSession.ReplacedBySessionId);
        Assert.Equal(2, repository.Sessions.Count);
    }

    [Fact]
    public async Task RefreshAsync_WhenOldTokenIsReused_ShouldRevokeAllUserSessions()
    {
        var repository = new FakeUserRepository();
        var service = CreateService(repository, new FakePasswordHasher());
        var registration = await service.RegisterAsync(
            new RegisterCommand("person@example.com", "Valid-Password-42!"),
            CancellationToken.None);
        var originalToken = registration.Response!.RefreshToken;

        var firstRefresh = await service.RefreshAsync(
            new RefreshCommand(originalToken),
            CancellationToken.None);
        Assert.True(firstRefresh.Succeeded);

        var replay = await service.RefreshAsync(
            new RefreshCommand(originalToken),
            CancellationToken.None);

        Assert.Equal(AuthenticationFailure.InvalidRefreshToken, replay.Failure);
        Assert.All(repository.Sessions, session => Assert.NotNull(session.RevokedAt));
    }

    [Fact]
    public async Task LogoutAsync_ShouldRevokeMatchingSession()
    {
        var repository = new FakeUserRepository();
        var service = CreateService(repository, new FakePasswordHasher());
        var registration = await service.RegisterAsync(
            new RegisterCommand("person@example.com", "Valid-Password-42!"),
            CancellationToken.None);
        var session = Assert.Single(repository.Sessions);

        await service.LogoutAsync(
            new LogoutCommand(registration.Response!.RefreshToken),
            CancellationToken.None);

        Assert.NotNull(session.RevokedAt);
    }

    private static AuthenticationService CreateService(
        FakeUserRepository repository,
        FakePasswordHasher hasher) =>
        new(
            repository,
            repository,
            repository,
            hasher,
            new FakeAccessTokenGenerator(),
            new FakeRefreshTokenService(),
            TimeProvider.System);

    private sealed class FakeUserRepository : IUserRepository, IUserSessionRepository, IUnitOfWork
    {
        public AppUser? User { get; set; }
        public UserCredential? Credential { get; set; }
        public int SaveCount { get; private set; }
        public List<UserSession> Sessions { get; } = [];

        public Task<AppUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(User?.Id == userId ? User : null);

        public Task<AppUser?> GetByNormalizedEmailAsync(
            string normalizedEmail,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                User?.NormalizedEmail == normalizedEmail ? User : null);

        public Task<UserCredential?> GetCredentialByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Credential?.UserId == userId ? Credential : null);

        public Task<UserSession?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken cancellationToken) =>
            Task.FromResult(Sessions.SingleOrDefault(session => session.RefreshTokenHash == refreshTokenHash));

        public Task AddAsync(UserSession session, CancellationToken cancellationToken)
        {
            Sessions.Add(session);
            return Task.CompletedTask;
        }

        public Task RevokeAllForUserAsync(Guid userId, DateTimeOffset revokedAt, CancellationToken cancellationToken)
        {
            foreach (var session in Sessions.Where(session => session.UserId == userId))
                session.Revoke(revokedAt);
            return Task.CompletedTask;
        }

        public Task AddUserAsync(AppUser user, CancellationToken cancellationToken)
        {
            User = user;
            return Task.CompletedTask;
        }

        public Task AddCredentialAsync(
            UserCredential credential,
            CancellationToken cancellationToken)
        {
            Credential = credential;
            return Task.CompletedTask;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public PasswordVerificationResult VerificationResult { get; init; } =
            PasswordVerificationResult.Success;

        public string Hash(string password) => "fake-hash";

        public PasswordVerificationResult Verify(string password, string encodedHash) =>
            VerificationResult;
    }

    private sealed class FakeRefreshTokenService : IRefreshTokenService
    {
        public string GenerateToken() => Guid.NewGuid().ToString("N");
        public string ComputeHash(string token) => "hash:" + token;
    }

    private sealed class FakeAccessTokenGenerator : IAccessTokenGenerator
    {
        public (string Token, DateTime ExpiresAtUtc) CreateToken(Guid userId, string email) =>
            ("test-access-token", DateTime.UtcNow.AddMinutes(15));
    }
}
