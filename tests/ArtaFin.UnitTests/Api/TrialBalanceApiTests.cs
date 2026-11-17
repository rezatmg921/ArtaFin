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

public sealed class TrialBalanceApiTests
{
    private const string Issuer = "ArtaFin.TrialBalanceTests";
    private const string Audience = "ArtaFin.TrialBalanceTests.Client";
    private const string SigningKey = "trial-balance-test-signing-key-at-least-32-bytes";

    [Fact]
    public async Task GetTrialBalance_WhenOwnerRequestsReport_ReturnsAccountBalances()
    {
        var ownerId = Guid.NewGuid();
        var book = FinancialBook.Create(Guid.NewGuid(), ownerId, "Trial balance API test", "USD", DateTimeOffset.UtcNow);
        var asOf = DateTimeOffset.UtcNow.AddDays(-1);
        var repository = new FakeTrialBalanceRepository(
        [
            new TrialBalanceAccountSnapshot(Guid.NewGuid(), "1000", "Cash", AccountType.Asset, 150m, 40m),
            new TrialBalanceAccountSnapshot(Guid.NewGuid(), "4000", "Sales", AccountType.Income, 0m, 150m)
        ]);
        using var factory = new ApiFactory(book, repository);
        using var client = CreateAuthenticatedClient(factory, ownerId);

        using var response = await client.GetAsync(
            $"/api/financial-books/{book.Id}/reports/trial-balance?asOf={Uri.EscapeDataString(asOf.ToString("O"))}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<TrialBalanceResponse>();
        Assert.NotNull(payload);
        Assert.Equal("USD", payload.Currency);
        Assert.Equal(asOf, payload.AsOf);
        Assert.Equal(110m, payload.TotalDebitBalance);
        Assert.Equal(150m, payload.TotalCreditBalance);
        Assert.Equal(110m, payload.Items[0].DebitBalance);
        Assert.Equal(150m, payload.Items[1].CreditBalance);
        Assert.Equal(asOf, repository.LastAsOf);
    }

    [Fact]
    public async Task GetTrialBalance_WithoutToken_ReturnsUnauthorized()
    {
        var book = FinancialBook.Create(Guid.NewGuid(), Guid.NewGuid(), "Trial balance API test", "USD", DateTimeOffset.UtcNow);
        var repository = new FakeTrialBalanceRepository([]);
        using var factory = new ApiFactory(book, repository);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/financial-books/{book.Id}/reports/trial-balance");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, repository.Calls);
    }

    private static HttpClient CreateAuthenticatedClient(ApiFactory factory, Guid userId)
    {
        var client = factory.CreateClient();
        var token = factory.Services.GetRequiredService<IAccessTokenGenerator>()
            .CreateToken(userId, "trial-balance-api-test@artafin.local").Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private sealed class ApiFactory(
        FinancialBook book,
        FakeTrialBalanceRepository repository) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Authentication:Jwt:Issuer"] = Issuer,
                    ["Authentication:Jwt:Audience"] = Audience,
                    ["Authentication:Jwt:SigningKey"] = SigningKey,
                    ["Authentication:Jwt:AccessTokenMinutes"] = "15",
                    ["ConnectionStrings:ArtaFin"] =
                        "Server=localhost;Database=ArtaFin_TrialBalanceApiTests;User ID=sa;Password=NotUsed_OnlyForHostStartup!;TrustServerCertificate=True"
                }));

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFinancialBookRepository>();
                services.RemoveAll<ITrialBalanceReadRepository>();
                services.AddSingleton<IFinancialBookRepository>(new FakeBookRepository(book));
                services.AddSingleton<ITrialBalanceReadRepository>(repository);
            });
        }
    }

    private sealed class FakeBookRepository(FinancialBook book) : IFinancialBookRepository
    {
        public Task<FinancialBook?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<FinancialBook?>(id == book.Id ? book : null);
        public Task<FinancialBookMember?> GetMemberAsync(Guid bookId, Guid userId, CancellationToken ct) =>
            Task.FromResult<FinancialBookMember?>(null);
        public Task<FinancialBookMember?> GetMemberByIdAsync(Guid bookId, Guid memberId, CancellationToken ct) =>
            Task.FromResult<FinancialBookMember?>(null);
        public Task<IReadOnlyList<FinancialBookMemberSummary>> GetActiveMembersAsync(Guid bookId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<FinancialBookMemberSummary>>([]);
        public Task AddMemberAsync(FinancialBookMember member, CancellationToken ct) =>
            throw new NotImplementedException();
    }

    private sealed class FakeTrialBalanceRepository(
        IReadOnlyList<TrialBalanceAccountSnapshot> accounts) : ITrialBalanceReadRepository
    {
        public int Calls { get; private set; }
        public DateTimeOffset? LastAsOf { get; private set; }

        public Task<IReadOnlyList<TrialBalanceAccountSnapshot>> GetAsOfAsync(
            Guid bookId, DateTimeOffset asOf, CancellationToken ct)
        {
            Calls++;
            LastAsOf = asOf;
            return Task.FromResult(accounts);
        }
    }

    private sealed record TrialBalanceResponse(
        DateTimeOffset AsOf,
        string Currency,
        IReadOnlyList<TrialBalanceItemResponse> Items,
        decimal TotalDebitBalance,
        decimal TotalCreditBalance);

    private sealed record TrialBalanceItemResponse(
        Guid AccountId, string Code, string Name, string Type,
        decimal TotalDebits, decimal TotalCredits,
        decimal DebitBalance, decimal CreditBalance);
}
