using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Domain.Finance;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace ArtaFin.UnitTests.Api;

public sealed class FinancialBookApiTests
{
    private const string Issuer = "ArtaFin.IntegrationTests";
    private const string Audience = "ArtaFin.IntegrationTests.Client";
    private const string SigningKey = "integration-test-signing-key-at-least-32-bytes";

    [Fact]
    public async Task CreateFinancialBook_WithoutAccessToken_ReturnsUnauthorized()
    {
        using var factory = new FinancialBookApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/financial-books",
            new { name = "Operating ledger", baseCurrency = "EUR" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(factory.Writer.AddedBook);
    }

    [Fact]
    public async Task CreateFinancialBook_WhenAuthenticated_CreatesBookForTokenOwner()
    {
        var userId = Guid.NewGuid();
        using var factory = new FinancialBookApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAccessToken(factory, userId));

        using var response = await client.PostAsJsonAsync(
            "/api/financial-books",
            new
            {
                name = "Operating ledger",
                baseCurrency = "eur",
                ownerId = Guid.NewGuid()
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(factory.Writer.AddedBook);
        Assert.Equal(userId, factory.Writer.AddedBook!.OwnerId);
        Assert.Equal("EUR", factory.Writer.AddedBook.BaseCurrency.Code);
        Assert.Equal(1, factory.UnitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateFinancialBook_WithUnsupportedCurrency_ReturnsValidationProblem()
    {
        var userId = Guid.NewGuid();
        using var factory = new FinancialBookApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAccessToken(factory, userId));

        using var response = await client.PostAsJsonAsync(
            "/api/financial-books",
            new { name = "Operating ledger", baseCurrency = "ZZZ" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(factory.Writer.AddedBook);
        Assert.Equal(0, factory.UnitOfWork.SaveChangesCalls);
    }

    private static string CreateAccessToken(FinancialBookApiFactory factory, Guid userId) =>
        factory.Services.GetRequiredService<IAccessTokenGenerator>()
            .CreateToken(userId, "financial-book-api-test@artafin.local").Token;

    private sealed class FinancialBookApiFactory : WebApplicationFactory<Program>
    {
        public FakeFinancialBookWriter Writer { get; } = new();
        public FakeUnitOfWork UnitOfWork { get; } = new();

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
                        "Server=localhost;Database=ArtaFin_FinancialBookApiTests;User ID=sa;Password=NotUsed_OnlyForHostStartup!;TrustServerCertificate=True"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFinancialBookWriter>();
                services.RemoveAll<IUnitOfWork>();
                services.AddSingleton<IFinancialBookWriter>(Writer);
                services.AddSingleton<IUnitOfWork>(UnitOfWork);
            });
        }
    }

    private sealed class FakeFinancialBookWriter : IFinancialBookWriter
    {
        public FinancialBook? AddedBook { get; private set; }

        public Task AddAsync(FinancialBook financialBook, CancellationToken cancellationToken)
        {
            AddedBook = financialBook;
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
