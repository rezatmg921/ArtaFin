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

public sealed class AccountLedgerApiTests
{
    private const string Issuer = "ArtaFin.AccountLedgerTests";
    private const string Audience = "ArtaFin.AccountLedgerTests.Client";
    private const string SigningKey = "account-ledger-test-signing-key-at-least-32-bytes";

    [Fact]
    public async Task GetAccountLedger_WhenOwnerRequestsPage_ReturnsLedgerAndBalance()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var account = CreateCashAccount(book.Id);
        var repository = new FakeAccountLedgerReadRepository(new AccountLedgerPage(
            [
                new AccountLedgerLine(
                    Guid.NewGuid(), DateTimeOffset.UtcNow, "Cash received", null, 1,
                    PostingSide.Debit, 100m, "USD", 100m)
            ],
            TotalCount: 1,
            OpeningBalance: 0m));
        using var factory = new AccountLedgerApiFactory(book, account, repository);
        using var client = CreateAuthenticatedClient(factory, ownerId);

        using var response = await client.GetAsync(
            $"/api/financial-books/{book.Id}/accounts/{account.Id}/ledger?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<AccountLedgerResponse>();
        Assert.NotNull(payload);
        Assert.Equal(account.Id, payload.AccountId);
        Assert.Equal("1000", payload.AccountCode);
        Assert.Equal("USD", payload.Currency);
        Assert.Equal(0m, payload.OpeningBalance);
        Assert.Equal(1, payload.TotalCount);
        Assert.Equal(100m, Assert.Single(payload.Items).BalanceAfter);
        Assert.Equal(AccountNormalBalance.Debit, repository.NormalBalance);
        Assert.Equal(account.Id, repository.AccountId);
    }

    [Fact]
    public async Task GetAccountLedger_WithoutAccessToken_ReturnsUnauthorized()
    {
        var book = CreateBook(Guid.NewGuid());
        var account = CreateCashAccount(book.Id);
        var repository = new FakeAccountLedgerReadRepository(new AccountLedgerPage([], 0, 0m));
        using var factory = new AccountLedgerApiFactory(book, account, repository);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/financial-books/{book.Id}/accounts/{account.Id}/ledger");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task GetAccountLedger_WhenDateRangeIsInvalid_ReturnsValidationProblem()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var account = CreateCashAccount(book.Id);
        var repository = new FakeAccountLedgerReadRepository(new AccountLedgerPage([], 0, 0m));
        using var factory = new AccountLedgerApiFactory(book, account, repository);
        using var client = CreateAuthenticatedClient(factory, ownerId);

        using var response = await client.GetAsync(
            $"/api/financial-books/{book.Id}/accounts/{account.Id}/ledger?from=2026-11-16T00:00:00Z&to=2026-11-15T00:00:00Z");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task GetAccountLedger_WhenFromEqualsTo_ReturnsValidationProblem()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var account = CreateCashAccount(book.Id);
        var repository = new FakeAccountLedgerReadRepository(new AccountLedgerPage([], 0, 0m));
        using var factory = new AccountLedgerApiFactory(book, account, repository);
        using var client = CreateAuthenticatedClient(factory, ownerId);

        using var response = await client.GetAsync(
            $"/api/financial-books/{book.Id}/accounts/{account.Id}/ledger?from=2026-11-16T00:00:00Z&to=2026-11-16T00:00:00Z");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, repository.Calls);
    }

    private static FinancialBook CreateBook(Guid ownerId) =>
        FinancialBook.Create(Guid.NewGuid(), ownerId, "Account ledger API test", "USD", DateTimeOffset.UtcNow);

    private static Account CreateCashAccount(Guid bookId) =>
        Account.Create(Guid.NewGuid(), bookId, "1000", "Cash", AccountType.Asset, DateTimeOffset.UtcNow);

    private static HttpClient CreateAuthenticatedClient(AccountLedgerApiFactory factory, Guid userId)
    {
        var client = factory.CreateClient();
        var token = factory.Services.GetRequiredService<IAccessTokenGenerator>()
            .CreateToken(userId, "account-ledger-api-test@artafin.local").Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private sealed class AccountLedgerApiFactory(
        FinancialBook book,
        Account account,
        FakeAccountLedgerReadRepository ledgerRepository) : WebApplicationFactory<Program>
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
                        "Server=localhost;Database=ArtaFin_AccountLedgerApiTests;User ID=sa;Password=NotUsed_OnlyForHostStartup!;TrustServerCertificate=True"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.UseTestPermissionAuthorization();
                services.RemoveAll<IFinancialBookRepository>();
                services.RemoveAll<IAccountRepository>();
                services.RemoveAll<IAccountLedgerReadRepository>();
                services.AddSingleton<IFinancialBookRepository>(new FakeFinancialBookRepository(book));
                services.AddSingleton<IAccountRepository>(new FakeAccountRepository(account));
                services.AddSingleton<IAccountLedgerReadRepository>(ledgerRepository);
            });
        }
    }

    private sealed class FakeFinancialBookRepository(FinancialBook book) : IFinancialBookRepository
    {
        public Task<FinancialBook?> GetByIdAsync(Guid financialBookId, CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBook?>(book.Id == financialBookId ? book : null);

        public Task<FinancialBookMember?> GetMemberAsync(
            Guid financialBookId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBookMember?>(null);

        public Task<FinancialBookMember?> GetMemberByIdAsync(
            Guid financialBookId, Guid membershipId, CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBookMember?>(null);

        public Task<IReadOnlyList<FinancialBookMemberSummary>> GetActiveMembersAsync(
            Guid financialBookId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FinancialBookMemberSummary>>([]);

        public Task AddMemberAsync(FinancialBookMember member, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    private sealed class FakeAccountRepository(Account account) : IAccountRepository
    {
        public Task<Account?> GetByIdAsync(
            Guid financialBookId, Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult<Account?>(
                account.FinancialBookId == financialBookId && account.Id == accountId ? account : null);

        public Task<AccountReadSnapshot?> GetReadSnapshotByIdAsync(
            Guid financialBookId, Guid accountId, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<AccountReadSnapshot?> GetForUpdateAsync(
            Guid financialBookId, Guid accountId, byte[]? expectedRowVersion, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<bool> ExistsByCodeAsync(
            Guid financialBookId, string normalizedCode, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<bool> DeleteIfUnusedAsync(Account value, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task AddAsync(Account value, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    private sealed class FakeAccountLedgerReadRepository(AccountLedgerPage page)
        : IAccountLedgerReadRepository
    {
        public int Calls { get; private set; }
        public Guid AccountId { get; private set; }
        public AccountNormalBalance NormalBalance { get; private set; }

        public Task<AccountLedgerPage> GetPageAsync(
            Guid financialBookId,
            Guid accountId,
            AccountNormalBalance normalBalance,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken)
        {
            Calls++;
            AccountId = accountId;
            NormalBalance = normalBalance;
            return Task.FromResult(page);
        }
    }

    private sealed record AccountLedgerResponse(
        Guid AccountId,
        string AccountCode,
        string AccountName,
        string AccountType,
        string NormalBalance,
        string Currency,
        int Page,
        int PageSize,
        int TotalCount,
        decimal OpeningBalance,
        IReadOnlyList<AccountLedgerLineResponse> Items);

    private sealed record AccountLedgerLineResponse(
        Guid JournalEntryId,
        DateTimeOffset PostedAt,
        string Description,
        Guid? ReversesJournalEntryId,
        int LineNumber,
        string Side,
        decimal Amount,
        string Currency,
        decimal BalanceAfter);
}
