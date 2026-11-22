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

public sealed class JournalEntryListApiTests
{
    private const string Issuer = "ArtaFin.JournalEntryListTests";
    private const string Audience = "ArtaFin.JournalEntryListTests.Client";
    private const string SigningKey = "journal-entry-list-test-signing-key-at-least-32-bytes";

    [Fact]
    public async Task ListJournalEntries_WhenOwnerRequestsPage_ReturnsPagedResults()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var entryId = Guid.NewGuid();
        var repository = new FakeJournalEntryReadRepository(new JournalEntryPage(
            [
                new JournalEntryListItem(entryId, DateTimeOffset.UtcNow, "Office supplies", null, 2),
                new JournalEntryListItem(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1), "Office rent", null, 2)
            ],
            2));
        using var factory = new JournalEntryListApiFactory(book, repository);
        using var client = CreateAuthenticatedClient(factory, ownerId);

        using var response = await client.GetAsync(
            $"/api/financial-books/{book.Id}/journal-entries?page=1&pageSize=10&search=office&isReversal=false");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JournalEntryListResponse>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload.Page);
        Assert.Equal(10, payload.PageSize);
        Assert.Equal(2, payload.TotalCount);
        Assert.Equal(2, payload.Items.Count);
        Assert.Equal("office", repository.SearchTerm);
        Assert.False(repository.IsReversal);
        Assert.Equal(book.Id, repository.FinancialBookId);
    }

    [Fact]
    public async Task ListJournalEntries_WithoutAccessToken_ReturnsUnauthorized()
    {
        var book = CreateBook(Guid.NewGuid());
        var repository = new FakeJournalEntryReadRepository(new JournalEntryPage([], 0));
        using var factory = new JournalEntryListApiFactory(book, repository);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/financial-books/{book.Id}/journal-entries");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task ListJournalEntries_WhenDateRangeIsInvalid_ReturnsValidationProblem()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var repository = new FakeJournalEntryReadRepository(new JournalEntryPage([], 0));
        using var factory = new JournalEntryListApiFactory(book, repository);
        using var client = CreateAuthenticatedClient(factory, ownerId);

        using var response = await client.GetAsync(
            $"/api/financial-books/{book.Id}/journal-entries?from=2026-11-15T00:00:00Z&to=2026-11-14T00:00:00Z");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, repository.Calls);
    }

    private static FinancialBook CreateBook(Guid ownerId) =>
        FinancialBook.Create(Guid.NewGuid(), ownerId, "Journal list API test", "USD", DateTimeOffset.UtcNow);

    private static HttpClient CreateAuthenticatedClient(
        JournalEntryListApiFactory factory,
        Guid userId)
    {
        var client = factory.CreateClient();
        var token = factory.Services.GetRequiredService<IAccessTokenGenerator>()
            .CreateToken(userId, "journal-list-api-test@artafin.local").Token;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private sealed class JournalEntryListApiFactory(
        FinancialBook book,
        FakeJournalEntryReadRepository repository) : WebApplicationFactory<Program>
    {
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
                        "Server=localhost;Database=ArtaFin_JournalEntryListTests;User ID=sa;Password=NotUsed_OnlyForHostStartup!;TrustServerCertificate=True"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.UseTestPermissionAuthorization();
                services.RemoveAll<IFinancialBookRepository>();
                services.RemoveAll<IJournalEntryReadRepository>();
                services.AddSingleton<IFinancialBookRepository>(
                    new FakeFinancialBookRepository(book));
                services.AddSingleton<IJournalEntryReadRepository>(repository);
            });
        }
    }

    private sealed class FakeFinancialBookRepository(FinancialBook book)
        : IFinancialBookRepository
    {
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

        public Task<FinancialBookMember?> GetMemberByIdAsync(
            Guid financialBookId,
            Guid membershipId,
            CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBookMember?>(null);

        public Task<IReadOnlyList<FinancialBookMemberSummary>> GetActiveMembersAsync(
            Guid financialBookId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FinancialBookMemberSummary>>(
                Array.Empty<FinancialBookMemberSummary>());

        public Task AddMemberAsync(
            FinancialBookMember member,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    private sealed class FakeJournalEntryReadRepository(JournalEntryPage page)
        : IJournalEntryReadRepository
    {
        public int Calls { get; private set; }
        public Guid FinancialBookId { get; private set; }
        public string? SearchTerm { get; private set; }
        public bool? IsReversal { get; private set; }

        public Task<JournalEntryPage> GetPageAsync(
            Guid financialBookId,
            DateTimeOffset? from,
            DateTimeOffset? to,
            string? searchTerm,
            bool? isReversal,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken)
        {
            Calls++;
            FinancialBookId = financialBookId;
            SearchTerm = searchTerm;
            IsReversal = isReversal;
            return Task.FromResult(page);
        }
    }

    private sealed record JournalEntryListResponse(
        int Page,
        int PageSize,
        int TotalCount,
        IReadOnlyList<JournalEntryListItemResponse> Items);

    private sealed record JournalEntryListItemResponse(
        Guid Id,
        DateTimeOffset PostedAt,
        string Description,
        Guid? ReversesJournalEntryId,
        int LineCount);
}
