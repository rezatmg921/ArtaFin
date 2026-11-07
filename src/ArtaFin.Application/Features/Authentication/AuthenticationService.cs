using System.Net.Mail;
using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Domain.Security;

namespace ArtaFin.Application.Features.Authentication;

/// <summary>
/// Implements local registration and password authentication without ASP.NET Core Identity.
/// </summary>
public sealed class AuthenticationService(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IAccessTokenGenerator accessTokenGenerator,
    TimeProvider timeProvider)
{
    private const int MaximumFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

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

        var normalizedEmail = email!.ToUpperInvariant();
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
        var user = AppUser.Register(Guid.NewGuid(), email, now);
        var passwordHash = passwordHasher.Hash(password!);
        var credential = UserCredential.Create(
            Guid.NewGuid(),
            user.Id,
            passwordHash,
            Pbkdf2AlgorithmId,
            now);

        await userRepository.AddUserAsync(user, cancellationToken);
        await userRepository.AddCredentialAsync(credential, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var (token, expiresAtUtc) = accessTokenGenerator.CreateToken(user.Id, user.Email);
        return AuthenticationResult.Success(new AuthenticationResponse(token, expiresAtUtc));
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
            credential.ReplacePassword(
                passwordHasher.Hash(command.Password),
                Pbkdf2AlgorithmId,
                now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        else if (credential.FailedLoginCount > 0 || credential.LockedUntil is not null)
        {
            credential.ResetFailedLogins();
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var (token, expiresAtUtc) = accessTokenGenerator.CreateToken(user.Id, user.Email);
        return AuthenticationResult.Success(new AuthenticationResponse(token, expiresAtUtc));
    }

    private const string Pbkdf2AlgorithmId = "pbkdf2-sha256-v1";

    private static Dictionary<string, string[]> ValidateRegistration(string? email, string? password)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(email) || email.Length > AppUser.MaxEmailLength ||
            !IsValidEmail(email))
        {
            errors["email"] = ["A valid email address is required (maximum 320 characters)."];
        }

        if (string.IsNullOrEmpty(password) || password.Length < 12 ||
            !password.Any(char.IsUpper) ||
            !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) ||
            !password.Any(character => !char.IsLetterOrDigit(character)))
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
