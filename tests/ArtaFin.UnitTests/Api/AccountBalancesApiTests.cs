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

public sealed class AccountBalancesApiTests
{
    private const string Issuer = "ArtaFin.AccountBalancesTests";
    private const string Audience = "ArtaFin.AccountBalancesTests.Client";
    private const string SigningKey = "account-balances-test-signing-key-at-least-32-bytes";

    [Fact]
    public async Task GetAccountBalances_WhenOwnerRequestsPage_ReturnsDerivedBalances()
    {
        var ownerId = Guid.NewGuid();
        var book = FinancialBook.Create(Guid.NewGuid(), ownerId, "Balances API test", "USD", DateTimeOffset.UtcNow);
        var repository = new FakeBalanceRepository(new AccountBalancePage(
            [
                new AccountBalanceSnapshot(Guid.NewGuid(), "1000", "Cash", AccountType.Asset,
                    AccountNormalBalance.Debit, AccountStatus.Active, 150m, 40m, 110m),
                new AccountBalanceSnapshot(Guid.NewGuid(), "4000", "Sales", AccountType.Income,
                    AccountNormalBalance.Credit, AccountStatus.Active, 0m, 150m, 150m)
            ], 2));
        using var factory = new ApiFactory(book, repository);
        using var client = CreateAuthenticatedClient(factory, ownerId);

        using var response = await client.GetAsync(
            $"/api/financial-books/{book.Id}/account-balances?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<BalancesResponse>();
        Assert.NotNull(payload);
        Assert.Equal("USD", payload.Currency);
        Assert.Equal(2, payload.TotalCount);
        Assert.Equal(110m, payload.Items[0].Balance);
        Assert.Equal(150m, payload.Items[1].Balance);
        Assert.Equal(1, repository.Calls);
    }

    [Fact]
    public async Task GetAccountBalances_WithoutToken_ReturnsUnauthorized()
    {
        var book = FinancialBook.Create(Guid.NewGuid(), Guid.NewGuid(), "Balances API test", "USD", DateTimeOffset.UtcNow);
        var repository = new FakeBalanceRepository(new AccountBalancePage([], 0));
        using var factory = new ApiFactory(book, repository);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/financial-books/{book.Id}/account-balances");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, repository.Calls);
    }

    private static HttpClient CreateAuthenticatedClient(ApiFactory factory, Guid userId)
    {
        var client = factory.CreateClient();
        var token = factory.Services.GetRequiredService<IAccessTokenGenerator>()
            .CreateToken(userId, "account-balances-api-test@artafin.local").Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private sealed class ApiFactory(
        FinancialBook book,
        FakeBalanceRepository balanceRepository) : WebApplicationFactory<Program>
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
                        "Server=localhost;Database=ArtaFin_AccountBalancesApiTests;User ID=sa;Password=NotUsed_OnlyForHostStartup!;TrustServerCertificate=True"
                }));

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFinancialBookRepository>();
                services.RemoveAll<IAccountBalanceReadRepository>();
                services.AddSingleton<IFinancialBookRepository>(new FakeBookRepository(book));
                services.AddSingleton<IAccountBalanceReadRepository>(balanceRepository);
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

    private sealed class FakeBalanceRepository(AccountBalancePage page) : IAccountBalanceReadRepository
    {
        public int Calls { get; private set; }
        public Task<AccountBalancePage> GetPageAsync(Guid bookId, int pageNumber, int pageSize, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(page);
        }
    }

    private sealed record BalancesResponse(
        int Page, int PageSize, int TotalCount, string Currency,
        IReadOnlyList<BalanceItemResponse> Items);
    private sealed record BalanceItemResponse(
        Guid AccountId, string Code, string Name, string Type, string NormalBalance,
        string Status, decimal TotalDebits, decimal TotalCredits, decimal Balance);
}
