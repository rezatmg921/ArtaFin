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

public sealed class ExpenseApiTests
{
    private const string Issuer = "ArtaFin.IntegrationTests";
    private const string Audience = "ArtaFin.IntegrationTests.Client";
    private const string SigningKey = "integration-test-signing-key-at-least-32-bytes";

    [Fact]
    public async Task CreateExpense_WhenTokenBelongsToOwner_ReturnsCreatedAndStoresBalancedEntry()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        using var factory = new ExpenseApiFactory(book);
        using var client = CreateAuthenticatedClient(factory, ownerId);

        using var response = await client.PostAsJsonAsync(
            $"/api/financial-books/{book.Id}/expenses",
            CreateValidRequest(factory));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<CreateExpenseResponse>();
        Assert.NotNull(payload);
        Assert.Equal(factory.JournalEntries.AddedEntry!.Id, payload.JournalEntryId);
        Assert.Equal("Office supplies", factory.JournalEntries.AddedEntry.Description);
        Assert.Equal(2, factory.JournalEntries.AddedEntry.Lines.Count);
        Assert.Equal(factory.ExpenseAccount.Id, factory.JournalEntries.AddedEntry.Lines[0].AccountId);
        Assert.Equal(PostingSide.Debit, factory.JournalEntries.AddedEntry.Lines[0].Side);
        Assert.Equal(factory.PaymentAccount.Id, factory.JournalEntries.AddedEntry.Lines[1].AccountId);
        Assert.Equal(PostingSide.Credit, factory.JournalEntries.AddedEntry.Lines[1].Side);
        Assert.Equal(125m, factory.JournalEntries.AddedEntry.Lines[0].Amount.Amount);
        Assert.Equal(1, factory.UnitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateExpense_WithoutAccessToken_ReturnsUnauthorized()
    {
        var book = CreateBook(Guid.NewGuid());
        using var factory = new ExpenseApiFactory(book);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/financial-books/{book.Id}/expenses",
            CreateValidRequest(factory));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(factory.JournalEntries.AddedEntry);
        Assert.Equal(0, factory.UnitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateExpense_WithNonPositiveAmount_ReturnsValidationProblem()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        using var factory = new ExpenseApiFactory(book);
        using var client = CreateAuthenticatedClient(factory, ownerId);

        using var response = await client.PostAsJsonAsync(
            $"/api/financial-books/{book.Id}/expenses",
            new
            {
                expenseAccountId = factory.ExpenseAccount.Id,
                paymentAccountId = factory.PaymentAccount.Id,
                description = "Office supplies",
                amount = 0m,
                currency = "EUR"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(factory.JournalEntries.AddedEntry);
        Assert.Equal(0, factory.UnitOfWork.SaveChangesCalls);
    }

    private static object CreateValidRequest(ExpenseApiFactory factory) => new
    {
        expenseAccountId = factory.ExpenseAccount.Id,
        paymentAccountId = factory.PaymentAccount.Id,
        description = "Office supplies",
        amount = 125m,
        currency = "EUR"
    };

    private static FinancialBook CreateBook(Guid ownerId) =>
        FinancialBook.Create(Guid.NewGuid(), ownerId, "Expense API test", "EUR", DateTimeOffset.UtcNow);

    private static HttpClient CreateAuthenticatedClient(ExpenseApiFactory factory, Guid userId)
    {
        var client = factory.CreateClient();
        var token = factory.Services.GetRequiredService<IAccessTokenGenerator>()
            .CreateToken(userId, "expense-api-test@artafin.local").Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private sealed class ExpenseApiFactory(FinancialBook book) : WebApplicationFactory<Program>
    {
        public Account ExpenseAccount { get; } =
            Account.Create(Guid.NewGuid(), book.Id, "6000", "Office supplies", AccountType.Expense, DateTimeOffset.UtcNow);
        public Account PaymentAccount { get; } =
            Account.Create(Guid.NewGuid(), book.Id, "1000", "Bank", AccountType.Asset, DateTimeOffset.UtcNow);
        public FakeJournalEntryRepository JournalEntries { get; } = new();
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
                        "Server=localhost;Database=ArtaFin_ExpenseApiTests;User ID=sa;Password=NotUsed_OnlyForHostStartup!;TrustServerCertificate=True"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFinancialBookRepository>();
                services.RemoveAll<IAccountRepository>();
                services.RemoveAll<IJournalEntryRepository>();
                services.RemoveAll<IUnitOfWork>();

                services.AddSingleton<IFinancialBookRepository>(new FakeFinancialBookRepository(book));
                services.AddSingleton<IAccountRepository>(
                    new FakeAccountRepository(ExpenseAccount, PaymentAccount));
                services.AddSingleton<IJournalEntryRepository>(JournalEntries);
                services.AddSingleton<IUnitOfWork>(UnitOfWork);
            });
        }
    }

    private sealed class FakeFinancialBookRepository(FinancialBook book) : IFinancialBookRepository
    {
        public Task<FinancialBook?> GetByIdAsync(Guid financialBookId, CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBook?>(book.Id == financialBookId ? book : null);
        public Task<FinancialBookMember?> GetMemberAsync(Guid financialBookId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBookMember?>(null);
        public Task<FinancialBookMember?> GetMemberByIdAsync(Guid financialBookId, Guid membershipId, CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBookMember?>(null);
        public Task<IReadOnlyList<FinancialBookMemberSummary>> GetActiveMembersAsync(Guid financialBookId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FinancialBookMemberSummary>>(Array.Empty<FinancialBookMemberSummary>());
        public Task AddMemberAsync(FinancialBookMember member, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    private sealed class FakeAccountRepository(params Account[] accounts) : IAccountRepository
    {
        public Task<Account?> GetByIdAsync(Guid financialBookId, Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult(accounts.SingleOrDefault(x => x.FinancialBookId == financialBookId && x.Id == accountId));
        public Task<AccountReadSnapshot?> GetReadSnapshotByIdAsync(Guid financialBookId, Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult<AccountReadSnapshot?>(null);
        public Task<AccountReadSnapshot?> GetForUpdateAsync(Guid financialBookId, Guid accountId, byte[]? expectedRowVersion, CancellationToken cancellationToken) =>
            Task.FromResult<AccountReadSnapshot?>(null);
        public Task<bool> ExistsByCodeAsync(Guid financialBookId, string normalizedCode, CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task<bool> DeleteIfUnusedAsync(Account account, CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task AddAsync(Account account, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    private sealed class FakeJournalEntryRepository : IJournalEntryRepository
    {
        public JournalEntry? AddedEntry { get; private set; }
        public Task<JournalEntry?> GetByIdAsync(Guid financialBookId, Guid journalEntryId, CancellationToken cancellationToken) =>
            Task.FromResult<JournalEntry?>(null);
        public Task<bool> HasReversalAsync(Guid financialBookId, Guid originalJournalEntryId, CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task AddAsync(JournalEntry entry, CancellationToken cancellationToken)
        {
            AddedEntry = entry;
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

    private sealed record CreateExpenseResponse(Guid JournalEntryId);
}
