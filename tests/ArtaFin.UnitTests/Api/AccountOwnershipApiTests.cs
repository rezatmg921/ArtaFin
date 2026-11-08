using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Domain.Finance;
using ArtaFin.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace ArtaFin.UnitTests.Api;

public sealed class AccountOwnershipApiTests
{
    private const string Issuer = "ArtaFin.IntegrationTests";
    private const string Audience = "ArtaFin.IntegrationTests.Client";
    private const string SigningKey = "integration-test-signing-key-at-least-32-bytes";

    [Fact]
    public async Task CreateAccount_WithoutAccessToken_ReturnsUnauthorized()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        using var factory = new OwnershipApiFactory(book);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/financial-books/{book.Id}/accounts",
            new { code = "1000", name = "Cash", type = "Asset" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateAccount_WhenTokenBelongsToOwner_ReturnsCreated()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        using var factory = new OwnershipApiFactory(book);
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAccessToken(ownerId));

        using var response = await client.PostAsJsonAsync(
            $"/api/financial-books/{book.Id}/accounts",
            new { code = "1000", name = "Cash", type = "Asset" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(factory.Accounts.AddedAccount);
        Assert.Equal(book.Id, factory.Accounts.AddedAccount!.FinancialBookId);
    }

    [Fact]
    public async Task CreateAccount_WhenNonOwnerForgesOwnerIdInBody_ReturnsNotFound()
    {
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        using var factory = new OwnershipApiFactory(book);
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAccessToken(attackerId));

        using var response = await client.PostAsJsonAsync(
            $"/api/financial-books/{book.Id}/accounts",
            new
            {
                code = "1000",
                name = "Cash",
                type = "Asset",
                userId = ownerId
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(factory.Accounts.AddedAccount);
        Assert.Equal(0, factory.UnitOfWork.SaveChangesCalls);
    }

    private static FinancialBook CreateBook(Guid ownerId) =>
        FinancialBook.Create(
            Guid.NewGuid(),
            ownerId,
            "API ownership test",
            "USD",
            DateTimeOffset.UtcNow);

    private static string CreateAccessToken(Guid userId)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: [new Claim("sub", userId.ToString())],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class OwnershipApiFactory : WebApplicationFactory<Program>
    {
        private readonly FinancialBook _book;

        public FakeAccountRepository Accounts { get; } = new();
        public FakeUnitOfWork UnitOfWork { get; } = new();

        public OwnershipApiFactory(FinancialBook book) => _book = book;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Authentication:Jwt:Issuer"] = Issuer,
                    ["Authentication:Jwt:Audience"] = Audience,
                    ["Authentication:Jwt:SigningKey"] = SigningKey,
                    ["Authentication:Jwt:AccessTokenMinutes"] = "15",
                    ["ConnectionStrings:ArtaFin"] =
                        "Server=localhost;Database=ArtaFin_OwnershipTests;User ID=sa;Password=NotUsed_OnlyForHostStartup!;TrustServerCertificate=True"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFinancialBookRepository>();
                services.RemoveAll<IAccountRepository>();
                services.RemoveAll<IUnitOfWork>();

                services.AddSingleton<IFinancialBookRepository>(
                    new FakeFinancialBookRepository(_book));
                services.AddSingleton<IAccountRepository>(Accounts);
                services.AddSingleton<IUnitOfWork>(UnitOfWork);
            });
        }
    }

    private sealed class FakeFinancialBookRepository(FinancialBook book)
        : IFinancialBookRepository
    {
        public Task<IReadOnlyList<FinancialBookMemberSummary>> GetActiveMembersAsync(
    Guid financialBookId,
    CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<FinancialBookMemberSummary>>(
                Array.Empty<FinancialBookMemberSummary>());
        }
        public Task AddMemberAsync(FinancialBookMember member, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<FinancialBook?> GetByIdAsync(
            Guid financialBookId,
            CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBook?>(
                book.Id == financialBookId ? book : null);

        public Task<FinancialBookMember?> GetMemberAsync(
            Guid financialBookId,
            Guid userId,
            CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBookMember?>(null);

        public Task<IReadOnlyList<FinancialBookMemberSummary>> GetActiveMembersAsync(
            Guid financialBookId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FinancialBookMemberSummary>>(
                Array.Empty<FinancialBookMemberSummary>());
    }

    private sealed class FakeAccountRepository : IAccountRepository
    {
        public Account? AddedAccount { get; private set; }

        public Task<Account?> GetByIdAsync(
            Guid financialBookId,
            Guid accountId,
            CancellationToken cancellationToken) =>
            Task.FromResult<Account?>(null);

        public Task<bool> ExistsByCodeAsync(
            Guid financialBookId,
            string normalizedCode,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task AddAsync(Account account, CancellationToken cancellationToken)
        {
            AddedAccount = account;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveChangesCalls++;
            return Task.FromResult(1);
        }
    }
}
