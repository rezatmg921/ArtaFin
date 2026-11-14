using System.Data.Common;
using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Domain.Finance;
using ArtaFin.Domain.Security;
using ArtaFin.Infrastructure;
using ArtaFin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ArtaFin.Infrastructure.IntegrationTests;

public sealed class FinancialBookMemberPersistenceTests : IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly DbContextOptions<ArtaFinDbContext> _options;

    public FinancialBookMemberPersistenceTests()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var configuredConnectionString =
            configuration.GetConnectionString("ArtaFin")
            ?? Environment.GetEnvironmentVariable("ARTAFIN_SQLSERVER");

        if (string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            throw new InvalidOperationException(
                "SQL Server connection string was not found. Add ConnectionStrings:ArtaFin " +
                "to tests/ArtaFin.Infrastructure.IntegrationTests/appsettings.Local.json, " +
                "or set the ConnectionStrings__ArtaFin environment variable.");
        }

        var builder = new DbConnectionStringBuilder { ConnectionString = configuredConnectionString };
        builder["Initial Catalog"] = $"ArtaFin_FinancialBookMember_{Guid.NewGuid():N}";
        _connectionString = builder.ConnectionString;
        _options = new DbContextOptionsBuilder<ArtaFinDbContext>()
            .UseSqlServer(_connectionString)
            .Options;
    }

    public async Task InitializeAsync()
    {
        await using var context = new ArtaFinDbContext(_options);
        await context.Database.MigrateAsync();
    }

    [Fact]
    public async Task SaveAndReload_PreservesMembershipProperties()
    {
        var now = DateTimeOffset.UtcNow;
        var bookId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        await using (var write = new ArtaFinDbContext(_options))
        {
            write.FinancialBooks.Add(FinancialBook.Create(
                bookId, Guid.NewGuid(), "Membership persistence", "USD", now));
            write.Users.Add(AppUser.Register(userId, "member@example.com", now));
            write.FinancialBookMembers.Add(FinancialBookMember.Grant(
                memberId, bookId, userId, FinancialBookRole.Accountant, now));
            await write.SaveChangesAsync();
        }

        await using var read = new ArtaFinDbContext(_options);
        var actual = await read.FinancialBookMembers
            .AsNoTracking()
            .SingleAsync(member => member.Id == memberId);

        Assert.Equal(bookId, actual.FinancialBookId);
        Assert.Equal(userId, actual.UserId);
        Assert.Equal(FinancialBookRole.Accountant, actual.Role);
        Assert.Equal(now, actual.GrantedAt);
        Assert.Null(actual.RevokedAt);
    }

    [Fact]
    public async Task Repository_AddMemberAndGetMember_PersistsAndFindsMembership()
    {
        var now = DateTimeOffset.UtcNow;
        var bookId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        await using (var setup = new ArtaFinDbContext(_options))
        {
            setup.FinancialBooks.Add(FinancialBook.Create(
                bookId, Guid.NewGuid(), "Repository membership", "USD", now));
            setup.Users.Add(AppUser.Register(userId, "repository@example.com", now));
            await setup.SaveChangesAsync();
        }

        var servicesConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ArtaFin"] = _connectionString
            })
            .Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(servicesConfiguration);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var repository = scope.ServiceProvider.GetRequiredService<IFinancialBookRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var member = FinancialBookMember.Grant(
            memberId, bookId, userId, FinancialBookRole.Viewer, now);

        await repository.AddMemberAsync(member, CancellationToken.None);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        var actual = await repository.GetMemberAsync(
            bookId, userId, CancellationToken.None);

        Assert.NotNull(actual);
        Assert.Equal(memberId, actual.Id);
        Assert.Equal(FinancialBookRole.Viewer, actual.Role);
    }

    [Fact]
    public async Task DuplicateMembership_IsRejectedByUniqueDatabaseIndex()
    {
        var now = DateTimeOffset.UtcNow;
        var bookId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using var context = new ArtaFinDbContext(_options);
        context.FinancialBooks.Add(FinancialBook.Create(
            bookId, Guid.NewGuid(), "Duplicate membership", "USD", now));
        context.Users.Add(AppUser.Register(userId, "duplicate@example.com", now));
        context.FinancialBookMembers.AddRange(
            FinancialBookMember.Grant(
                Guid.NewGuid(), bookId, userId, FinancialBookRole.Accountant, now),
            FinancialBookMember.Grant(
                Guid.NewGuid(), bookId, userId, FinancialBookRole.Auditor, now.AddMinutes(1)));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Membership_RejectsUnknownFinancialBook()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();

        await using var context = new ArtaFinDbContext(_options);
        context.Users.Add(AppUser.Register(userId, "valid-user@example.com", now));
        context.FinancialBookMembers.Add(FinancialBookMember.Grant(
            Guid.NewGuid(), Guid.NewGuid(), userId, FinancialBookRole.Viewer, now));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Membership_RejectsUnknownUser()
    {
        var now = DateTimeOffset.UtcNow;
        var bookId = Guid.NewGuid();

        await using var context = new ArtaFinDbContext(_options);
        context.FinancialBooks.Add(FinancialBook.Create(
            bookId, Guid.NewGuid(), "Missing user", "USD", now));
        context.FinancialBookMembers.Add(FinancialBookMember.Grant(
            Guid.NewGuid(), bookId, Guid.NewGuid(), FinancialBookRole.Viewer, now));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task GetActiveMembers_FiltersRevokedMembersAndUsesStableOrdering()
    {
        var now = DateTimeOffset.UtcNow;
        var bookId = Guid.NewGuid();
        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();
        var revokedUserId = Guid.NewGuid();
        var firstMemberId = Guid.NewGuid();
        var secondMemberId = Guid.NewGuid();

        await using (var setup = new ArtaFinDbContext(_options))
        {
            setup.FinancialBooks.Add(FinancialBook.Create(
                bookId, Guid.NewGuid(), "Active member listing", "USD", now));
            setup.Users.AddRange(
                AppUser.Register(firstUserId, "zeta@example.com", now),
                AppUser.Register(secondUserId, "alpha@example.com", now),
                AppUser.Register(revokedUserId, "revoked@example.com", now));

            var revokedMember = FinancialBookMember.Grant(
                Guid.NewGuid(), bookId, revokedUserId, FinancialBookRole.Viewer, now);
            revokedMember.Revoke(now.AddMinutes(1));

            setup.FinancialBookMembers.AddRange(
                FinancialBookMember.Grant(
                    firstMemberId, bookId, firstUserId, FinancialBookRole.Accountant, now),
                FinancialBookMember.Grant(
                    secondMemberId, bookId, secondUserId, FinancialBookRole.Auditor, now),
                revokedMember);
            await setup.SaveChangesAsync();
        }

        var servicesConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ArtaFin"] = _connectionString
            })
            .Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(servicesConfiguration);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IFinancialBookRepository>();

        var members = await repository.GetActiveMembersAsync(
            bookId, CancellationToken.None);

        Assert.Equal(2, members.Count);
        Assert.Equal(new[] { secondUserId, firstUserId }, members.Select(x => x.UserId).ToArray());
        Assert.Equal(new[] { "alpha@example.com", "zeta@example.com" },
            members.Select(x => x.Email).ToArray());
        Assert.Equal(new[] { FinancialBookRole.Auditor, FinancialBookRole.Accountant },
            members.Select(x => x.Role).ToArray());
    }

    public async Task DisposeAsync()
    {
        await using var context = new ArtaFinDbContext(_options);
        await context.Database.EnsureDeletedAsync();
    }
}
