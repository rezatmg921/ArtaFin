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

public sealed class BalanceSheetApiTests
{
    private const string Issuer = "ArtaFin.BalanceSheetTests";
    private const string Audience = "ArtaFin.BalanceSheetTests.Client";
    private const string SigningKey = "balance-sheet-test-signing-key-at-least-32-bytes";

    [Fact]
    public async Task GetBalanceSheet_WhenOwnerRequestsReport_ReturnsAssetsLiabilitiesAndUnclosedEarnings()
    {
        var ownerId = Guid.NewGuid();
        var book = FinancialBook.Create(Guid.NewGuid(), ownerId, "Balance sheet API test", "USD", DateTimeOffset.UtcNow);
        var asOf = DateTimeOffset.UtcNow.AddMinutes(-1);
        var repository = new FakeBalanceSheetRepository(
        [
            new BalanceSheetAccountSnapshot(Guid.NewGuid(), "1000", "Cash", AccountType.Asset, 200m, 0m, BalanceSheetClassification.Current),
            new BalanceSheetAccountSnapshot(Guid.NewGuid(), "1100", "Equipment", AccountType.Asset, 0m, 0m, BalanceSheetClassification.NonCurrent),
            new BalanceSheetAccountSnapshot(Guid.NewGuid(), "1200", "Other assets", AccountType.Asset, 0m, 0m),
            new BalanceSheetAccountSnapshot(Guid.NewGuid(), "2000", "Payables", AccountType.Liability, 0m, 50m, BalanceSheetClassification.Current),
            new BalanceSheetAccountSnapshot(Guid.NewGuid(), "2100", "Long-term payable", AccountType.Liability, 0m, 0m, BalanceSheetClassification.NonCurrent),
            new BalanceSheetAccountSnapshot(Guid.NewGuid(), "2200", "Other liabilities", AccountType.Liability, 0m, 0m),
            new BalanceSheetAccountSnapshot(Guid.NewGuid(), "3000", "Capital", AccountType.Equity, 0m, 30m),
            new BalanceSheetAccountSnapshot(Guid.NewGuid(), "4000", "Sales", AccountType.Income, 0m, 150m),
            new BalanceSheetAccountSnapshot(Guid.NewGuid(), "6000", "Supplies", AccountType.Expense, 30m, 0m)
        ]);
        using var factory = new ApiFactory(book, repository);
        using var client = CreateAuthenticatedClient(factory, ownerId);

        using var response = await client.GetAsync(
            $"/api/financial-books/{book.Id}/reports/balance-sheet?asOf={Uri.EscapeDataString(asOf.ToString("O"))}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<BalanceSheetResponse>();
        Assert.NotNull(payload);
        Assert.Equal("USD", payload.Currency);
        Assert.Equal(asOf, payload.AsOf);
        Assert.Equal(200m, payload.TotalAssets);
        Assert.Equal(50m, payload.TotalLiabilities);
        Assert.Equal(120m, payload.UnclosedNetIncome);
        Assert.Equal(150m, payload.TotalEquity);
        Assert.Equal(200m, payload.TotalLiabilitiesAndEquity);
        Assert.Equal(0m, payload.Difference);
        Assert.Equal(3, payload.Assets.Count);
        Assert.Single(payload.CurrentAssets);
        Assert.Single(payload.NonCurrentAssets);
        Assert.Single(payload.UnclassifiedAssets);
        Assert.Equal(200m, payload.CurrentAssetsTotal);
        Assert.Equal(0m, payload.NonCurrentAssetsTotal);
        Assert.Equal(0m, payload.UnclassifiedAssetsTotal);
        Assert.Single(payload.CurrentLiabilities);
        Assert.Single(payload.NonCurrentLiabilities);
        Assert.Single(payload.UnclassifiedLiabilities);
        Assert.Equal(50m, payload.CurrentLiabilitiesTotal);
        Assert.Equal(0m, payload.NonCurrentLiabilitiesTotal);
        Assert.Equal(0m, payload.UnclassifiedLiabilitiesTotal);
        Assert.Equal(asOf, repository.LastAsOf);
    }

    [Fact]
    public async Task GetBalanceSheet_WithoutToken_ReturnsUnauthorized()
    {
        var book = FinancialBook.Create(Guid.NewGuid(), Guid.NewGuid(), "Balance sheet API test", "USD", DateTimeOffset.UtcNow);
        var repository = new FakeBalanceSheetRepository([]);
        using var factory = new ApiFactory(book, repository);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/financial-books/{book.Id}/reports/balance-sheet");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, repository.Calls);
    }

    private static HttpClient CreateAuthenticatedClient(ApiFactory factory, Guid userId)
    {
        var client = factory.CreateClient();
        var token = factory.Services.GetRequiredService<IAccessTokenGenerator>()
            .CreateToken(userId, "balance-sheet-api-test@artafin.local").Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private sealed class ApiFactory(
        FinancialBook book,
        FakeBalanceSheetRepository repository) : WebApplicationFactory<Program>
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
                        "Server=localhost;Database=ArtaFin_BalanceSheetApiTests;User ID=sa;Password=NotUsed_OnlyForHostStartup!;TrustServerCertificate=True"
                }));

            builder.ConfigureTestServices(services =>
            {
                services.UseTestPermissionAuthorization();
                services.RemoveAll<IFinancialBookRepository>();
                services.RemoveAll<IBalanceSheetReadRepository>();
                services.RemoveAll<IObligationMaturityReadRepository>();
                services.AddSingleton<IFinancialBookRepository>(new FakeBookRepository(book));
                services.AddSingleton<IBalanceSheetReadRepository>(repository);
                services.AddSingleton<IObligationMaturityReadRepository>(
                    new FakeObligationMaturityReadRepository());
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

    private sealed class FakeBalanceSheetRepository(
        IReadOnlyList<BalanceSheetAccountSnapshot> accounts) : IBalanceSheetReadRepository
    {
        public int Calls { get; private set; }
        public DateTimeOffset? LastAsOf { get; private set; }

        public Task<IReadOnlyList<BalanceSheetAccountSnapshot>> GetAsOfAsync(
            Guid bookId, DateTimeOffset asOf, CancellationToken ct)
        {
            Calls++;
            LastAsOf = asOf;
            return Task.FromResult(accounts);
        }
    }

    private sealed class FakeObligationMaturityReadRepository
        : IObligationMaturityReadRepository
    {
        public Task<IReadOnlyList<ObligationMaturitySnapshot>> GetAsOfAsync(
            Guid financialBookId, DateTimeOffset asOf, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ObligationMaturitySnapshot>>([]);
    }

    private sealed record BalanceSheetResponse(
        DateTimeOffset AsOf,
        string Currency,
        IReadOnlyList<BalanceSheetItemResponse> Assets,
        IReadOnlyList<BalanceSheetItemResponse> Liabilities,
        IReadOnlyList<BalanceSheetItemResponse> Equity,
        decimal UnclosedNetIncome,
        decimal TotalAssets,
        decimal TotalLiabilities,
        decimal TotalEquity,
        decimal TotalLiabilitiesAndEquity,
        decimal Difference,
        IReadOnlyList<BalanceSheetItemResponse> CurrentAssets,
        IReadOnlyList<BalanceSheetItemResponse> NonCurrentAssets,
        IReadOnlyList<BalanceSheetItemResponse> UnclassifiedAssets,
        IReadOnlyList<BalanceSheetItemResponse> CurrentLiabilities,
        IReadOnlyList<BalanceSheetItemResponse> NonCurrentLiabilities,
        IReadOnlyList<BalanceSheetItemResponse> UnclassifiedLiabilities,
        decimal CurrentAssetsTotal,
        decimal NonCurrentAssetsTotal,
        decimal UnclassifiedAssetsTotal,
        decimal CurrentLiabilitiesTotal,
        decimal NonCurrentLiabilitiesTotal,
        decimal UnclassifiedLiabilitiesTotal);

    private sealed record BalanceSheetItemResponse(
        Guid AccountId, string Code, string Name, string Type, decimal Amount, string BalanceSheetClassification);
}
