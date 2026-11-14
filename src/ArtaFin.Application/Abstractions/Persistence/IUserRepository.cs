using ArtaFin.Domain.Security;

namespace ArtaFin.Application.Abstractions.Persistence;

/// <summary>Persistence port for local user accounts and their password credentials.</summary>
public interface IUserRepository
{
    Task<AppUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<AppUser?> GetByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken);

    Task<UserCredential?> GetCredentialByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task AddUserAsync(AppUser user, CancellationToken cancellationToken);

    Task AddCredentialAsync(UserCredential credential, CancellationToken cancellationToken);
}
