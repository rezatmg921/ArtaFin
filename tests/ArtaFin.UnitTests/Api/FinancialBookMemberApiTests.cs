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

public sealed class FinancialBookMemberApiTests
{
    private const string Issuer = "ArtaFin.IntegrationTests";
    private const string Audience = "ArtaFin.IntegrationTests.Client";
    private const string SigningKey = "integration-test-signing-key-at-least-32-bytes";

    [Fact]
    public async Task ChangeRole_WhenOwnerChangesActiveMember_ReturnsNoContentAndPersistsRole()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var member = CreateMember(book.Id);
        using var factory = new FinancialBookMemberApiFactory(book, member);
        using var client = CreateAuthenticatedClient(factory, ownerId);

        using var response = await client.PutAsJsonAsync(
            $"/api/financial-books/{book.Id}/members/{member.Id}/role",
            new { role = "Auditor" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(FinancialBookRole.Auditor, member.Role);
        Assert.Equal(1, factory.UnitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task ChangeRole_WhenCallerIsNotOwner_ReturnsNotFound()
    {
        var book = CreateBook(Guid.NewGuid());
        var member = CreateMember(book.Id);
        using var factory = new FinancialBookMemberApiFactory(book, member);
        using var client = CreateAuthenticatedClient(factory, Guid.NewGuid());

        using var response = await client.PutAsJsonAsync(
            $"/api/financial-books/{book.Id}/members/{member.Id}/role",
            new { role = "Viewer" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(FinancialBookRole.Accountant, member.Role);
        Assert.Equal(0, factory.UnitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task ChangeRole_WithUnknownRole_ReturnsBadRequest()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var member = CreateMember(book.Id);
        using var factory = new FinancialBookMemberApiFactory(book, member);
        using var client = CreateAuthenticatedClient(factory, ownerId);

        using var response = await client.PutAsJsonAsync(
            $"/api/financial-books/{book.Id}/members/{member.Id}/role",
            new { role = "Administrator" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(FinancialBookRole.Accountant, member.Role);
        Assert.Equal(0, factory.UnitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Revoke_WhenOwnerRevokesActiveMember_ReturnsNoContentAndRetainsMembership()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var member = CreateMember(book.Id);
        using var factory = new FinancialBookMemberApiFactory(book, member);
        using var client = CreateAuthenticatedClient(factory, ownerId);

        using var response = await client.DeleteAsync(
            $"/api/financial-books/{book.Id}/members/{member.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.NotNull(member.RevokedAt);
        Assert.Same(member, factory.Repository.Member);
        Assert.Equal(1, factory.UnitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Revoke_WithoutAccessToken_ReturnsUnauthorized()
    {
        var book = CreateBook(Guid.NewGuid());
        var member = CreateMember(book.Id);
        using var factory = new FinancialBookMemberApiFactory(book, member);
        using var client = factory.CreateClient();

        using var response = await client.DeleteAsync(
            $"/api/financial-books/{book.Id}/members/{member.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(member.RevokedAt);
        Assert.Equal(0, factory.UnitOfWork.SaveChangesCalls);
    }

    private static HttpClient CreateAuthenticatedClient(
        FinancialBookMemberApiFactory factory,
        Guid userId)
    {
        var client = factory.CreateClient();
        var token = factory.Services.GetRequiredService<IAccessTokenGenerator>()
            .CreateToken(userId, "financial-book-member-api-test@artafin.local").Token;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static FinancialBook CreateBook(Guid ownerId) =>
        FinancialBook.Create(
            Guid.NewGuid(), ownerId, "Member API test", "EUR", DateTimeOffset.UtcNow);

    private static FinancialBookMember CreateMember(Guid bookId) =>
        FinancialBookMember.Grant(
            Guid.NewGuid(), bookId, Guid.NewGuid(), FinancialBookRole.Accountant,
            DateTimeOffset.UtcNow.AddMinutes(-5));

    private sealed class FinancialBookMemberApiFactory(
        FinancialBook book,
        FinancialBookMember member) : WebApplicationFactory<Program>
    {
        public FakeFinancialBookRepository Repository { get; } = new(book, member);
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
                        "Server=localhost;Database=ArtaFin_FinancialBookMemberApiTests;User ID=sa;Password=NotUsed_OnlyForHostStartup!;TrustServerCertificate=True"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFinancialBookRepository>();
                services.RemoveAll<IUnitOfWork>();
                services.AddSingleton<IFinancialBookRepository>(Repository);
                services.AddSingleton<IUnitOfWork>(UnitOfWork);
            });
        }
    }

    private sealed class FakeFinancialBookRepository(
        FinancialBook book,
        FinancialBookMember member) : IFinancialBookRepository
    {
        public FinancialBookMember Member => member;

        public Task<FinancialBook?> GetByIdAsync(
            Guid financialBookId,
            CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBook?>(book.Id == financialBookId ? book : null);

        public Task<FinancialBookMember?> GetMemberAsync(
            Guid financialBookId,
            Guid userId,
            CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBookMember?>(
                member.FinancialBookId == financialBookId && member.UserId == userId
                    ? member
                    : null);

        public Task<FinancialBookMember?> GetMemberByIdAsync(
            Guid financialBookId,
            Guid membershipId,
            CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBookMember?>(
                member.FinancialBookId == financialBookId && member.Id == membershipId
                    ? member
                    : null);

        public Task<IReadOnlyList<FinancialBookMemberSummary>> GetActiveMembersAsync(
            Guid financialBookId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FinancialBookMemberSummary>>(
                Array.Empty<FinancialBookMemberSummary>());

        public Task AddMemberAsync(
            FinancialBookMember newMember,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
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
