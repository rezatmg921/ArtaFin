using System.Data;
using System.Net.Mail;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Infrastructure.Persistence;

/// <summary>
/// Seeds the built-in roles and creates the explicitly configured first administrator.
/// This operation is opt-in, transactional, and safe to repeat after a successful run.
/// </summary>
public sealed class InitialDataSeeder(
    ArtaFinDbContext dbContext,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider)
{
    private const string AdministratorRoleName = "Administrator";
    private const string UserRoleName = "User";
    private const string PasswordAlgorithm = "pbkdf2-sha256-v1";

    public async Task SeedAsync(
        string administratorEmail,
        string administratorPassword,
        CancellationToken cancellationToken = default)
    {
        ValidateAdministratorCredentials(administratorEmail, administratorPassword);

        var email = administratorEmail.Trim();
        var normalizedEmail = email.ToUpperInvariant();
        var now = timeProvider.GetUtcNow();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var roles = await dbContext.Roles
            .Where(role => role.NormalizedName == AdministratorRoleName.ToUpperInvariant() ||
                           role.NormalizedName == UserRoleName.ToUpperInvariant())
            .ToListAsync(cancellationToken);

        var administratorRole = roles.SingleOrDefault(
            role => role.NormalizedName == AdministratorRoleName.ToUpperInvariant());

        if (administratorRole is null)
        {
            administratorRole = AppRole.Create(
                Guid.NewGuid(),
                AdministratorRoleName,
                "Full system administration role.",
                now);
            await dbContext.Roles.AddAsync(administratorRole, cancellationToken);
        }

        if (!roles.Any(role => role.NormalizedName == UserRoleName.ToUpperInvariant()))
        {
            await dbContext.Roles.AddAsync(
                AppRole.Create(Guid.NewGuid(), UserRoleName, "Standard application user role.", now),
                cancellationToken);
        }

        var existingAdministrator = await dbContext.UserRoles
            .AnyAsync(userRole => userRole.RoleId == administratorRole.Id, cancellationToken);

        var existingUser = await dbContext.Users
            .SingleOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);

        if (existingAdministrator)
        {
            if (existingUser is null ||
                !await dbContext.UserRoles.AnyAsync(
                    userRole => userRole.UserId == existingUser.Id &&
                                userRole.RoleId == administratorRole.Id,
                    cancellationToken))
            {
                throw new InvalidOperationException(
                    "An administrator is already configured. The bootstrap email does not match that administrator; refusing to create or promote another account.");
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        if (existingUser is not null)
        {
            throw new InvalidOperationException(
                "The configured bootstrap email already belongs to a non-administrator account. Refusing to promote it automatically. Configure a new, unused bootstrap email.");
        }

        var administrator = AppUser.Register(Guid.NewGuid(), email, now);
        var credential = UserCredential.Create(
            Guid.NewGuid(),
            administrator.Id,
            passwordHasher.Hash(administratorPassword),
            PasswordAlgorithm,
            now);
        var assignment = UserRole.Assign(
            Guid.NewGuid(),
            administrator.Id,
            administratorRole.Id,
            now);

        await dbContext.Users.AddAsync(administrator, cancellationToken);
        await dbContext.UserCredentials.AddAsync(credential, cancellationToken);
        await dbContext.UserRoles.AddAsync(assignment, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static void ValidateAdministratorCredentials(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) ||
            email.Length > AppUser.MaxEmailLength ||
            !IsValidEmail(email.Trim()))
        {
            throw new InvalidOperationException(
                "Bootstrap administrator email is missing or invalid. Set BootstrapAdmin__Email.");
        }

        if (string.IsNullOrEmpty(password) ||
            password.Length < 12 ||
            !password.Any(char.IsUpper) ||
            !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) ||
            !password.Any(character => !char.IsLetterOrDigit(character)))
        {
            throw new InvalidOperationException(
                "Bootstrap administrator password must be at least 12 characters and include uppercase, lowercase, numeric, and non-alphanumeric characters. Set BootstrapAdmin__Password securely.");
        }
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
