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

    public async Task<IReadOnlyList<string>> GetRoleNamesByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.UserRoles
            .Where(userRole => userRole.UserId == userId)
            .Join(
                dbContext.Roles,
                userRole => userRole.RoleId,
                role => role.Id,
                (_, role) => role.Name)
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid?> GetRoleIdByNormalizedNameAsync(
        string normalizedName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedName);
        return await dbContext.Roles
            .Where(role => role.NormalizedName == normalizedName)
            .Select(role => (Guid?)role.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task AddUserRoleAsync(UserRole userRole, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userRole);
        await dbContext.UserRoles.AddAsync(userRole, cancellationToken);
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
