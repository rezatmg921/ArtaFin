using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Features.Reports.GetCashFlowReconciliation;
using ArtaFin.Application.Features.Reports.GetCashFlowStatement;
using ArtaFin.Application.Features.Reports.GetTrialBalance;
using ArtaFin.Domain.Finance;
using MediatR;
using Xunit;

namespace ArtaFin.UnitTests;

public sealed class CashFlowReconciliationQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReconcilesExternalFlowsAfterAggregatingSplitInternalTransfers()
    {
        var from = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddMonths(1);
        var accountId = Guid.NewGuid();
        var account = Account(accountId, "1000", "Cash");
        var sender = new TestSender(
            Balance(from.AddTicks(-1), account with { TotalDebits = 100m, TotalCredits = 100m }),
            Balance(to.AddTicks(-1), account with { TotalDebits = 150m, TotalCredits = 150m }),
            CashFlow(from, to, 35m, 35m, true,
                Transfer(7m), Transfer(8m)));
        var repository = new TestCashFlowStatementReadRepository(Snapshot(
            CashLine(accountId, "1000", from.AddDays(2))));
        var handler = new GetCashFlowReconciliationQueryHandler(sender, repository);

        var result = await handler.Handle(
            new GetCashFlowReconciliationQuery(Guid.NewGuid(), from, to), CancellationToken.None);

        Assert.True(result.IsReconciled);
        Assert.Equal(50m, result.CashAccountDebits);
        Assert.Equal(50m, result.CashAccountCredits);
        Assert.Equal(15m, result.InternalTransfers);
        Assert.Equal(35m, result.ExternalCashInflowsFromTrialBalance);
        Assert.Equal(35m, result.ExternalCashOutflowsFromTrialBalance);
        Assert.Equal(0m, result.ReceiptsDifference);
        Assert.Equal(0m, result.PaymentsDifference);
        Assert.Equal(to, result.TrialBalanceAsOf);
        Assert.Equal(new[] { from.AddTicks(-1), to.AddTicks(-1) }, sender.TrialBalanceCutoffs);
    }

    [Fact]
    public async Task Handle_IncludesCashAccountCreatedDuringPeriod()
    {
        var from = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddMonths(1);
        var cashId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        var cash = Account(cashId, "1000", "Cash");
        var bank = Account(bankId, "1001", "Bank");
        var sender = new TestSender(
            Balance(from.AddTicks(-1), cash with { TotalDebits = 10m, TotalCredits = 10m }),
            Balance(to.AddTicks(-1),
                cash with { TotalDebits = 40m, TotalCredits = 40m },
                bank with { TotalDebits = 20m, TotalCredits = 20m }),
            CashFlow(from, to, 40m, 40m, true, Transfer(10m)));
        var repository = new TestCashFlowStatementReadRepository(Snapshot(
            CashLine(cashId, "1000", from.AddDays(1)),
            CashLine(bankId, "1001", from.AddDays(2))));
        var handler = new GetCashFlowReconciliationQueryHandler(sender, repository);

        var result = await handler.Handle(
            new GetCashFlowReconciliationQuery(Guid.NewGuid(), from, to), CancellationToken.None);

        Assert.True(result.IsReconciled);
        Assert.Equal(2, result.Accounts.Count);
        Assert.Contains(result.Accounts, item => item.AccountId == bankId &&
            item.Debits == 20m && item.Credits == 20m);
        Assert.Equal(50m, result.CashAccountDebits);
        Assert.Equal(40m, result.ExternalCashInflowsFromTrialBalance);
    }

    [Fact]
    public async Task Handle_FailsWhenEitherSideDiffersOrCashFlowHasExceptions()
    {
        var from = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddMonths(1);
        var accountId = Guid.NewGuid();
        var account = Account(accountId, "1000", "Cash");
        var sender = new TestSender(
            Balance(from.AddTicks(-1), account),
            Balance(to.AddTicks(-1), account with { TotalDebits = 50m, TotalCredits = 45m }),
            CashFlow(from, to, 44m, 40m, false, Transfer(5m)));
        var repository = new TestCashFlowStatementReadRepository(Snapshot(
            CashLine(accountId, "1000", from.AddDays(1))));
        var handler = new GetCashFlowReconciliationQueryHandler(sender, repository);

        var result = await handler.Handle(
            new GetCashFlowReconciliationQuery(Guid.NewGuid(), from, to), CancellationToken.None);

        Assert.Equal(45m, result.ExternalCashInflowsFromTrialBalance);
        Assert.Equal(40m, result.ExternalCashOutflowsFromTrialBalance);
        Assert.Equal(1m, result.ReceiptsDifference);
        Assert.Equal(0m, result.PaymentsDifference);
        Assert.False(result.IsReconciled);
    }

    [Fact]
    public async Task Handle_RejectsInvalidPeriod()
    {
        var instant = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var handler = new GetCashFlowReconciliationQueryHandler(
            new TestSender(Balance(instant, Account(Guid.NewGuid(), "1000", "Cash")),
                Balance(instant, Account(Guid.NewGuid(), "1000", "Cash")),
                CashFlow(instant, instant.AddDays(1), 0m, 0m, true)),
            new TestCashFlowStatementReadRepository(Snapshot()));

        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(
            new GetCashFlowReconciliationQuery(Guid.NewGuid(), instant, instant),
            CancellationToken.None));
    }

    private static TrialBalanceAccountResult Account(
        Guid id, string code, string name) =>
        new(id, code, name, AccountType.Asset, 0m, 0m, 0m, 0m);

    private static TrialBalanceResult Balance(
        DateTimeOffset asOf, params TrialBalanceAccountResult[] items) =>
        new(asOf, "USD", items, items.Sum(item => item.DebitBalance),
            items.Sum(item => item.CreditBalance));

    private static CashFlowStatementResult CashFlow(
        DateTimeOffset from,
        DateTimeOffset to,
        decimal receipts,
        decimal payments,
        bool isReconciled,
        params CashFlowInternalTransferResult[] transfers)
    {
        var activity = new CashFlowActivityResult(
            CashFlowActivity.Operating, receipts, payments, receipts - payments);
        return new CashFlowStatementResult(
            from, to, "USD", 0m, [activity], [], receipts - payments,
            receipts - payments, 0m, isReconciled, [], transfers, []);
    }

    private static CashFlowInternalTransferResult Transfer(decimal amount) =>
        new(Guid.NewGuid(), new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.Zero),
            "Internal transfer", "1000", "Cash", "1001", "Bank", amount, "USD");

    private static CashFlowStatementReadSnapshot Snapshot(
        params CashFlowStatementLineSnapshot[] lines) =>
        new(lines, [], [], []);

    private static CashFlowStatementLineSnapshot CashLine(
        Guid accountId, string code, DateTimeOffset postedAt) =>
        new(Guid.NewGuid(), postedAt, "Cash movement", 1, accountId, code,
            code == "1000" ? "Cash" : "Bank", AccountType.Asset,
            CashFlowAccountType.Cash, PostingSide.Debit, 1m, "USD");

    private sealed class TestCashFlowStatementReadRepository(
        CashFlowStatementReadSnapshot snapshot) : ICashFlowStatementReadRepository
    {
        public Task<CashFlowStatementReadSnapshot> GetBeforeAsync(
            Guid financialBookId, DateTimeOffset to, CancellationToken cancellationToken) =>
            Task.FromResult(snapshot);
    }

    private sealed class TestSender(
        TrialBalanceResult opening,
        TrialBalanceResult closing,
        CashFlowStatementResult cashFlow) : ISender
    {
        public List<DateTimeOffset> TrialBalanceCutoffs { get; } = [];

        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            object response = request switch
            {
                GetTrialBalanceQuery query => GetTrialBalance(query),
                GetCashFlowStatementQuery => cashFlow,
                _ => throw new NotSupportedException($"Unexpected request: {request.GetType().Name}")
            };
            return Task.FromResult((TResponse)response);
        }

        private TrialBalanceResult GetTrialBalance(GetTrialBalanceQuery query)
        {
            var cutoff = query.AsOf!.Value;
            TrialBalanceCutoffs.Add(cutoff);
            return cutoff == opening.AsOf ? opening : closing;
        }

        public Task Send<TRequest>(
            TRequest request,
            CancellationToken cancellationToken = default)
            where TRequest : IRequest =>
            throw new NotSupportedException();

        public Task<object?> Send(
            object request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(
            object request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
