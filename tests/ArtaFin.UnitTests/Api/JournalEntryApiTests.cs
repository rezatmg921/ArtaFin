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

public sealed class JournalEntryApiTests
{
    private const string Issuer = "ArtaFin.IntegrationTests";
    private const string Audience = "ArtaFin.IntegrationTests.Client";
    private const string SigningKey = "integration-test-signing-key-at-least-32-bytes";

    [Fact]
    public async Task CreateJournalEntry_WithoutAccessToken_ReturnsUnauthorized()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        using var factory = new JournalEntryApiFactory(book);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/financial-books/{book.Id}/journal-entries",
            CreateValidRequest(factory));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(factory.JournalEntries.AddedEntry);
    }

    [Fact]
    public async Task CreateJournalEntry_WhenPersistenceConcurrencyOccurs_ReturnsConflict()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        using var factory = new JournalEntryApiFactory(book);
        factory.UnitOfWork.ThrowConcurrencyException = true;
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAccessToken(factory, ownerId));

        using var response = await client.PostAsJsonAsync(
            $"/api/financial-books/{book.Id}/journal-entries",
            CreateValidRequest(factory));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
        Assert.NotNull(problem);
        Assert.Equal(409, problem.Status);
        Assert.Equal("Concurrency conflict", problem.Title);
        Assert.Contains("reload and try again", problem.Detail);
    }

    [Fact]
    public async Task CreateJournalEntry_WhenTokenBelongsToOwner_ReturnsCreated()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        using var factory = new JournalEntryApiFactory(book);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAccessToken(factory, ownerId));

        using var response = await client.PostAsJsonAsync(
            $"/api/financial-books/{book.Id}/journal-entries",
            CreateValidRequest(factory));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(factory.JournalEntries.AddedEntry);
        Assert.Equal(book.Id, factory.JournalEntries.AddedEntry!.FinancialBookId);
        Assert.Equal(1, factory.UnitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateJournalEntry_WhenTokenBelongsToActiveAccountant_ReturnsCreated()
    {
        var userId = Guid.NewGuid();
        var book = CreateBook(Guid.NewGuid());
        var member = FinancialBookMember.Grant(
            Guid.NewGuid(), book.Id, userId, FinancialBookRole.Accountant,
            DateTimeOffset.UtcNow.AddMinutes(-5));
        using var factory = new JournalEntryApiFactory(book, member);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAccessToken(factory, userId));

        using var response = await client.PostAsJsonAsync(
            $"/api/financial-books/{book.Id}/journal-entries",
            CreateValidRequest(factory));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(factory.JournalEntries.AddedEntry);
        Assert.Equal(1, factory.UnitOfWork.SaveChangesCalls);
    }

    [Theory]
    [InlineData(FinancialBookRole.Auditor)]
    [InlineData(FinancialBookRole.Viewer)]
    public async Task CreateJournalEntry_WhenMemberRoleCannotPost_ReturnsNotFound(
        FinancialBookRole role)
    {
        var userId = Guid.NewGuid();
        var book = CreateBook(Guid.NewGuid());
        var member = FinancialBookMember.Grant(
            Guid.NewGuid(), book.Id, userId, role, DateTimeOffset.UtcNow.AddMinutes(-5));
        using var factory = new JournalEntryApiFactory(book, member);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAccessToken(factory, userId));

        using var response = await client.PostAsJsonAsync(
            $"/api/financial-books/{book.Id}/journal-entries",
            CreateValidRequest(factory));

        // The API deliberately hides inaccessible financial books with 404.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(factory.JournalEntries.AddedEntry);
        Assert.Equal(0, factory.UnitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateJournalEntry_WhenAccountantMembershipIsRevoked_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var book = CreateBook(Guid.NewGuid());
        var grantedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var member = FinancialBookMember.Grant(
            Guid.NewGuid(), book.Id, userId, FinancialBookRole.Accountant, grantedAt);
        member.Revoke(DateTimeOffset.UtcNow.AddMinutes(-1));

        using var factory = new JournalEntryApiFactory(book, member);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAccessToken(factory, userId));

        using var response = await client.PostAsJsonAsync(
            $"/api/financial-books/{book.Id}/journal-entries",
            CreateValidRequest(factory));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(factory.JournalEntries.AddedEntry);
        Assert.Equal(0, factory.UnitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateJournalEntry_WithUnbalancedLines_ReturnsBadRequest()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        using var factory = new JournalEntryApiFactory(book);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAccessToken(factory, ownerId));

        var request = new
        {
            description = "Unbalanced entry",
            lines = new[]
            {
                new { accountId = factory.DebitAccount.Id, side = "Debit", amount = 25m, currency = "EUR" },
                new { accountId = factory.CreditAccount.Id, side = "Credit", amount = 20m, currency = "EUR" }
            }
        };

        using var response = await client.PostAsJsonAsync(
            $"/api/financial-books/{book.Id}/journal-entries", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(factory.JournalEntries.AddedEntry);
        Assert.Equal(0, factory.UnitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateJournalEntry_WithInvalidRequest_ReturnsValidationProblem()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        using var factory = new JournalEntryApiFactory(book);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAccessToken(factory, ownerId));

        using var response = await client.PostAsJsonAsync(
            $"/api/financial-books/{book.Id}/journal-entries",
            new { description = "", lines = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(factory.JournalEntries.AddedEntry);
    }

    [Fact]
    public async Task ReverseJournalEntry_WhenTokenBelongsToOwner_ReturnsCreatedAndStoresReversal()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        using var factory = new JournalEntryApiFactory(book);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAccessToken(factory, ownerId));

        using var createResponse = await client.PostAsJsonAsync(
            $"/api/financial-books/{book.Id}/journal-entries",
            CreateValidRequest(factory));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var original = Assert.IsType<JournalEntry>(factory.JournalEntries.AddedEntry);
        using var reversalResponse = await client.PostAsJsonAsync(
            $"/api/financial-books/{book.Id}/journal-entries/{original.Id}/reversal",
            new { description = "Reverse office supplies" });

        Assert.Equal(HttpStatusCode.Created, reversalResponse.StatusCode);
        Assert.NotNull(factory.JournalEntries.AddedEntry);
        Assert.Equal(original.Id, factory.JournalEntries.AddedEntry!.ReversesJournalEntryId);
        Assert.Equal(2, factory.JournalEntries.AddedEntry.Lines.Count);
        Assert.Equal(2, factory.UnitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task GetJournalEntry_WhenTokenBelongsToOwner_ReturnsEntryAndLines()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        using var factory = new JournalEntryApiFactory(book);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAccessToken(factory, ownerId));

        using var createResponse = await client.PostAsJsonAsync(
            $"/api/financial-books/{book.Id}/journal-entries",
            CreateValidRequest(factory));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var entry = Assert.IsType<JournalEntry>(factory.JournalEntries.AddedEntry);
        using var response = await client.GetAsync(
            $"/api/financial-books/{book.Id}/journal-entries/{entry.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JournalEntryReadResponse>();
        Assert.NotNull(payload);
        Assert.Equal(entry.Id, payload.Id);
        Assert.Equal("Office supplies", payload.Description);
        Assert.Equal(2, payload.Lines.Count);
        Assert.Equal("Debit", payload.Lines[0].Side);
        Assert.Equal(25m, payload.Lines[0].Amount);
        Assert.Equal("EUR", payload.Lines[0].Currency);
    }

    private static object CreateValidRequest(JournalEntryApiFactory factory) => new
    {
        description = "Office supplies",
        lines = new[]
        {
            new { accountId = factory.DebitAccount.Id, side = "Debit", amount = 25m, currency = "EUR" },
            new { accountId = factory.CreditAccount.Id, side = "Credit", amount = 25m, currency = "EUR" }
        }
    };

    private static FinancialBook CreateBook(Guid ownerId) =>
        FinancialBook.Create(Guid.NewGuid(), ownerId, "Journal API test", "EUR", DateTimeOffset.UtcNow);

    private static string CreateAccessToken(JournalEntryApiFactory factory, Guid userId) =>
        factory.Services.GetRequiredService<IAccessTokenGenerator>()
            .CreateToken(userId, "journal-api-test@artafin.local").Token;

    private sealed class JournalEntryApiFactory : WebApplicationFactory<Program>
    {
        private readonly FinancialBook _book;
        private readonly FinancialBookMember? _member;
        public Account DebitAccount { get; }
        public Account CreditAccount { get; }
        public FakeJournalEntryRepository JournalEntries { get; } = new();
        public FakeUnitOfWork UnitOfWork { get; } = new();

        public JournalEntryApiFactory(FinancialBook book, FinancialBookMember? member = null)
        {
            _book = book;
            _member = member;
            var now = DateTimeOffset.UtcNow;
            DebitAccount = Account.Create(Guid.NewGuid(), book.Id, "1000", "Cash", AccountType.Asset, now);
            CreditAccount = Account.Create(Guid.NewGuid(), book.Id, "3000", "Capital", AccountType.Equity, now);
        }

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
                        "Server=localhost;Database=ArtaFin_JournalApiTests;User ID=sa;Password=NotUsed_OnlyForHostStartup!;TrustServerCertificate=True"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFinancialBookRepository>();
                services.RemoveAll<IAccountRepository>();
                services.RemoveAll<IJournalEntryRepository>();
                services.RemoveAll<IUnitOfWork>();

                services.AddSingleton<IFinancialBookRepository>(new FakeFinancialBookRepository(_book, _member));
                services.AddSingleton<IAccountRepository>(new FakeAccountRepository(DebitAccount, CreditAccount));
                services.AddSingleton<IJournalEntryRepository>(JournalEntries);
                services.AddSingleton<IUnitOfWork>(UnitOfWork);
            });
        }
    }

    private sealed class FakeFinancialBookRepository(
        FinancialBook book, FinancialBookMember? member) : IFinancialBookRepository
    {
        public Task<FinancialBook?> GetByIdAsync(Guid financialBookId, CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBook?>(book.Id == financialBookId ? book : null);
        public Task<FinancialBookMember?> GetMemberAsync(Guid financialBookId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBookMember?>(
                member?.FinancialBookId == financialBookId && member.UserId == userId ? member : null);
        public Task<IReadOnlyList<FinancialBookMemberSummary>> GetActiveMembersAsync(Guid financialBookId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FinancialBookMemberSummary>>(Array.Empty<FinancialBookMemberSummary>());
        public Task AddMemberAsync(FinancialBookMember member, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    private sealed class FakeAccountRepository(params Account[] accounts) : IAccountRepository
    {
        public Task<Account?> GetByIdAsync(Guid financialBookId, Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult(accounts.SingleOrDefault(x => x.FinancialBookId == financialBookId && x.Id == accountId));

        public Task<AccountReadSnapshot?> GetReadSnapshotByIdAsync(
            Guid financialBookId,
            Guid accountId,
            CancellationToken cancellationToken) =>
            Task.FromResult<AccountReadSnapshot?>(null);

        public Task<AccountReadSnapshot?> GetForUpdateAsync(
            Guid financialBookId,
            Guid accountId,
            byte[]? expectedRowVersion,
            CancellationToken cancellationToken) =>
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

        public Task<JournalEntry?> GetByIdAsync(
            Guid financialBookId,
            Guid journalEntryId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                AddedEntry is not null &&
                AddedEntry.FinancialBookId == financialBookId &&
                AddedEntry.Id == journalEntryId
                    ? AddedEntry
                    : null);

        public Task<bool> HasReversalAsync(
            Guid financialBookId,
            Guid originalJournalEntryId,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task AddAsync(JournalEntry entry, CancellationToken cancellationToken)
        {
            AddedEntry = entry;
            return Task.CompletedTask;
        }
    }

    private sealed record JournalEntryReadResponse(
        Guid Id,
        Guid FinancialBookId,
        DateTimeOffset PostedAt,
        string Description,
        Guid? ReversesJournalEntryId,
        IReadOnlyList<JournalEntryLineReadResponse> Lines);

    private sealed record JournalEntryLineReadResponse(
        int LineNumber,
        Guid AccountId,
        string Side,
        decimal Amount,
        string Currency);

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }
        public bool ThrowConcurrencyException { get; set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveChangesCalls++;
            if (ThrowConcurrencyException)
            {
                throw new PersistenceConcurrencyException(
                    "The resource was modified by another operation.",
                    new InvalidOperationException("Simulated concurrency conflict."));
            }

            return Task.FromResult(1);
        }
    }

    private sealed record ProblemDetailsResponse(int? Status, string? Title, string? Detail);
}
