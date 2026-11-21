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

public sealed class IncomeStatementApiTests
{
    private const string Issuer = "ArtaFin.IncomeStatementTests";
    private const string Audience = "ArtaFin.IncomeStatementTests.Client";
    private const string SigningKey = "income-statement-test-signing-key-at-least-32-bytes";

    [Fact]
    public async Task GetIncomeStatement_WhenOwnerRequestsPeriod_ReturnsIncomeExpensesAndNetProfit()
    {
        var ownerId = Guid.NewGuid();
        var book = FinancialBook.Create(Guid.NewGuid(), ownerId, "Income statement API test", "USD", DateTimeOffset.UtcNow);
        var from = DateTimeOffset.UtcNow.AddDays(-30);
        var to = DateTimeOffset.UtcNow;
        var repository = new FakeIncomeStatementRepository(
        [
            new IncomeStatementAccountSnapshot(Guid.NewGuid(), "4000", "Sales", AccountType.Income, 10m, 150m),
            new IncomeStatementAccountSnapshot(Guid.NewGuid(), "6000", "Supplies", AccountType.Expense, 40m, 5m),
            new IncomeStatementAccountSnapshot(Guid.NewGuid(), "6100", "Rent", AccountType.Expense, 0m, 0m)
        ]);
        using var factory = new ApiFactory(book, repository);
        using var client = CreateAuthenticatedClient(factory, ownerId);

        using var response = await client.GetAsync(
            $"/api/financial-books/{book.Id}/reports/income-statement?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<IncomeStatementResponse>();
        Assert.NotNull(payload);
        Assert.Equal("USD", payload.Currency);
        Assert.Equal(from, payload.From);
        Assert.Equal(to, payload.To);
        Assert.Equal(140m, payload.TotalIncome);
        Assert.Equal(35m, payload.TotalExpenses);
        Assert.Equal(105m, payload.NetProfit);
        Assert.Equal(2, payload.ExpenseAccounts.Count);
        Assert.Equal(0m, Assert.Single(payload.ExpenseAccounts, item => item.Code == "6100").Amount);
        Assert.Equal(from, repository.LastFrom);
        Assert.Equal(to, repository.LastTo);
    }

    [Fact]
    public async Task GetIncomeStatement_WhenPeriodIsInvalid_ReturnsValidationProblem()
    {
        var ownerId = Guid.NewGuid();
        var book = FinancialBook.Create(Guid.NewGuid(), ownerId, "Income statement API test", "USD", DateTimeOffset.UtcNow);
        var repository = new FakeIncomeStatementRepository([]);
        using var factory = new ApiFactory(book, repository);
        using var client = CreateAuthenticatedClient(factory, ownerId);

        using var response = await client.GetAsync(
            $"/api/financial-books/{book.Id}/reports/income-statement?from=2026-11-17T00:00:00Z&to=2026-11-17T00:00:00Z");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task GetIncomeStatement_WithoutToken_ReturnsUnauthorized()
    {
        var book = FinancialBook.Create(Guid.NewGuid(), Guid.NewGuid(), "Income statement API test", "USD", DateTimeOffset.UtcNow);
        var repository = new FakeIncomeStatementRepository([]);
        using var factory = new ApiFactory(book, repository);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/financial-books/{book.Id}/reports/income-statement?from=2026-11-01T00:00:00Z&to=2026-12-01T00:00:00Z");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, repository.Calls);
    }

    private static HttpClient CreateAuthenticatedClient(ApiFactory factory, Guid userId)
    {
        var client = factory.CreateClient();
        var token = factory.Services.GetRequiredService<IAccessTokenGenerator>()
            .CreateToken(userId, "income-statement-api-test@artafin.local").Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private sealed class ApiFactory(
        FinancialBook book,
        FakeIncomeStatementRepository repository) : WebApplicationFactory<Program>
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
                        "Server=localhost;Database=ArtaFin_IncomeStatementApiTests;User ID=sa;Password=NotUsed_OnlyForHostStartup!;TrustServerCertificate=True"
                }));

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFinancialBookRepository>();
                services.RemoveAll<IIncomeStatementReadRepository>();
                services.AddSingleton<IFinancialBookRepository>(new FakeBookRepository(book));
                services.AddSingleton<IIncomeStatementReadRepository>(repository);
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

    private sealed class FakeIncomeStatementRepository(
        IReadOnlyList<IncomeStatementAccountSnapshot> accounts) : IIncomeStatementReadRepository
    {
        public int Calls { get; private set; }
        public DateTimeOffset? LastFrom { get; private set; }
        public DateTimeOffset? LastTo { get; private set; }

        public Task<IReadOnlyList<IncomeStatementAccountSnapshot>> GetPeriodAsync(
            Guid bookId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
        {
            Calls++;
            LastFrom = from;
            LastTo = to;
            return Task.FromResult(accounts);
        }
    }

    private sealed record IncomeStatementResponse(
        DateTimeOffset From,
        DateTimeOffset To,
        string Currency,
        IReadOnlyList<IncomeStatementItemResponse> IncomeAccounts,
        IReadOnlyList<IncomeStatementItemResponse> ExpenseAccounts,
        decimal TotalIncome,
        decimal TotalExpenses,
        decimal NetProfit);

    private sealed record IncomeStatementItemResponse(
        Guid AccountId, string Code, string Name, string Type, decimal Amount);
}
