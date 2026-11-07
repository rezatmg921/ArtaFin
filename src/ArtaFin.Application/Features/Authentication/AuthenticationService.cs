using System.Net.Mail;
using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Domain.Security;

namespace ArtaFin.Application.Features.Authentication;

/// <summary>Local password authentication and refresh-token session lifecycle.</summary>
public sealed class AuthenticationService(
    IUserRepository userRepository,
    IUserSessionRepository sessionRepository,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IAccessTokenGenerator accessTokenGenerator,
    IRefreshTokenService refreshTokenService,
    TimeProvider timeProvider)
{
    private const int MaximumFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);
    private const string Pbkdf2AlgorithmId = "pbkdf2-sha256-v1";

    public async Task<AuthenticationResult> RegisterAsync(
        RegisterCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var email = command.Email?.Trim();
        var password = command.Password;
        var errors = ValidateRegistration(email, password);
        if (errors.Count > 0)
            return AuthenticationResult.Failed(AuthenticationFailure.Validation, errors);

        var validEmail = email!;
        var validPassword = password!;
        var normalizedEmail = validEmail.ToUpperInvariant();
        if (await userRepository.GetByNormalizedEmailAsync(normalizedEmail, cancellationToken) is not null)
        {
            return AuthenticationResult.Failed(
                AuthenticationFailure.EmailAlreadyRegistered,
                new Dictionary<string, string[]>
                {
                    ["email"] = ["An account with this email address already exists."]
                });
        }

        var now = timeProvider.GetUtcNow();
        var user = AppUser.Register(Guid.NewGuid(), validEmail, now);
        var credential = UserCredential.Create(
            Guid.NewGuid(),
            user.Id,
            passwordHasher.Hash(validPassword),
            Pbkdf2AlgorithmId,
            now);

        await userRepository.AddUserAsync(user, cancellationToken);
        await userRepository.AddCredentialAsync(credential, cancellationToken);
        var response = await CreateAuthenticationResponseAsync(user, now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return AuthenticationResult.Success(response);
    }

    public async Task<AuthenticationResult> LoginAsync(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.Email) || string.IsNullOrEmpty(command.Password))
            return AuthenticationResult.Failed(AuthenticationFailure.InvalidCredentials);

        var normalizedEmail = command.Email.Trim().ToUpperInvariant();
        var user = await userRepository.GetByNormalizedEmailAsync(normalizedEmail, cancellationToken);
        if (user is null)
            return AuthenticationResult.Failed(AuthenticationFailure.InvalidCredentials);
        if (!user.IsActive)
            return AuthenticationResult.Failed(AuthenticationFailure.AccountDisabled);

        var credential = await userRepository.GetCredentialByUserIdAsync(user.Id, cancellationToken);
        if (credential is null)
            return AuthenticationResult.Failed(AuthenticationFailure.InvalidCredentials);

        var now = timeProvider.GetUtcNow();
        if (credential.IsLockedAt(now))
            return AuthenticationResult.Failed(AuthenticationFailure.AccountLocked);

        var verification = passwordHasher.Verify(command.Password, credential.PasswordHash);
        if (verification == PasswordVerificationResult.Failed)
        {
            credential.RecordFailedLogin(MaximumFailedAttempts, now, LockoutDuration);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return credential.IsLockedAt(now)
                ? AuthenticationResult.Failed(AuthenticationFailure.AccountLocked)
                : AuthenticationResult.Failed(AuthenticationFailure.InvalidCredentials);
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            credential.ReplacePassword(passwordHasher.Hash(command.Password), Pbkdf2AlgorithmId, now);
        }
        else if (credential.FailedLoginCount > 0 || credential.LockedUntil is not null)
        {
            credential.ResetFailedLogins();
        }

        var response = await CreateAuthenticationResponseAsync(user, now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return AuthenticationResult.Success(response);
    }

    public async Task<AuthenticationResult> RefreshAsync(
        RefreshCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
            return AuthenticationResult.Failed(AuthenticationFailure.InvalidRefreshToken);

        var now = timeProvider.GetUtcNow();
        var hash = refreshTokenService.ComputeHash(command.RefreshToken);
        var oldSession = await sessionRepository.GetByRefreshTokenHashAsync(hash, cancellationToken);
        if (oldSession is null)
            return AuthenticationResult.Failed(AuthenticationFailure.InvalidRefreshToken);

        // A revoked token presented again is a replay signal: revoke every session for this user.
        if (oldSession.RevokedAt is not null)
        {
            await sessionRepository.RevokeAllForUserAsync(oldSession.UserId, now, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return AuthenticationResult.Failed(AuthenticationFailure.InvalidRefreshToken);
        }

        if (!oldSession.IsUsableAt(now))
        {
            oldSession.Revoke(now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return AuthenticationResult.Failed(AuthenticationFailure.InvalidRefreshToken);
        }

        var user = await userRepository.GetByIdAsync(oldSession.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            await sessionRepository.RevokeAllForUserAsync(oldSession.UserId, now, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return AuthenticationResult.Failed(AuthenticationFailure.InvalidRefreshToken);
        }

        var newSessionId = Guid.NewGuid();
        var rawRefreshToken = refreshTokenService.GenerateToken();
        var refreshExpiresAt = now.Add(RefreshTokenLifetime);
        var newSession = UserSession.Start(
            newSessionId,
            user.Id,
            refreshTokenService.ComputeHash(rawRefreshToken),
            now,
            refreshExpiresAt);

        oldSession.Revoke(now, newSessionId);
        await sessionRepository.AddAsync(newSession, cancellationToken);
        var (accessToken, accessExpiresAt) = accessTokenGenerator.CreateToken(user.Id, user.Email);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return AuthenticationResult.Success(new AuthenticationResponse(
            accessToken,
            accessExpiresAt,
            rawRefreshToken,
            refreshExpiresAt));
    }

    public async Task LogoutAsync(LogoutCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
            return;

        var hash = refreshTokenService.ComputeHash(command.RefreshToken);
        var session = await sessionRepository.GetByRefreshTokenHashAsync(hash, cancellationToken);
        if (session is null || session.RevokedAt is not null)
            return;

        session.Revoke(timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthenticationResponse> CreateAuthenticationResponseAsync(
        AppUser user,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var rawRefreshToken = refreshTokenService.GenerateToken();
        var refreshExpiresAt = now.Add(RefreshTokenLifetime);
        var session = UserSession.Start(
            Guid.NewGuid(),
            user.Id,
            refreshTokenService.ComputeHash(rawRefreshToken),
            now,
            refreshExpiresAt);
        await sessionRepository.AddAsync(session, cancellationToken);

        var (accessToken, accessExpiresAt) = accessTokenGenerator.CreateToken(user.Id, user.Email);
        return new AuthenticationResponse(
            accessToken,
            accessExpiresAt,
            rawRefreshToken,
            refreshExpiresAt);
    }

    private static Dictionary<string, string[]> ValidateRegistration(string? email, string? password)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(email) || email.Length > AppUser.MaxEmailLength || !IsValidEmail(email))
            errors["email"] = ["A valid email address is required (maximum 320 characters)."];

        if (string.IsNullOrEmpty(password) || password.Length < 12 ||
            !password.Any(char.IsUpper) || !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) || !password.Any(character => !char.IsLetterOrDigit(character)))
        {
            errors["password"] =
            [
                "Password must be at least 12 characters and include uppercase, lowercase, numeric, and non-alphanumeric characters."
            ];
        }

        return errors;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var address = new MailAddress(email);
            return string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
