using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(ArtaFinDbContext dbContext) : IUserRepository
{
    public Task<AppUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public Task<AppUser?> GetByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);

        return dbContext.Users.SingleOrDefaultAsync(
            user => user.NormalizedEmail == normalizedEmail,
            cancellationToken);
    }

    public Task<UserCredential?> GetCredentialByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return dbContext.UserCredentials.SingleOrDefaultAsync(
            credential => credential.UserId == userId,
            cancellationToken);
    }

    public async Task AddUserAsync(AppUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        await dbContext.Users.AddAsync(user, cancellationToken);
    }

    public async Task AddCredentialAsync(
        UserCredential credential,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credential);
        await dbContext.UserCredentials.AddAsync(credential, cancellationToken);
    }
}
