using ArtaFin.Domain.Common;
using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests;

public sealed class CashFlowNonCashDisclosureTests
{
    private static readonly Guid BookId = Guid.NewGuid();
    private static readonly Guid EntryId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(CashFlowActivity.Investing)]
    [InlineData(CashFlowActivity.Financing)]
    public void Create_AcceptsNonCashInvestingAndFinancingDisclosures(CashFlowActivity activity)
    {
        var disclosure = Create(activity);

        Assert.Equal(125m, disclosure.Amount);
        Assert.Equal("USD", disclosure.Currency);
        Assert.Equal(activity, disclosure.Activity);
        Assert.Equal("ASSET_ACQUIRED_THROUGH_LEASE", disclosure.Category);
        Assert.Equal("Lease commencement", disclosure.Memo);
        Assert.Equal(2, disclosure.SourceLineNumber);
    }

    [Theory]
    [InlineData(CashFlowActivity.Operating)]
    [InlineData((CashFlowActivity)0)]
    public void Create_RejectsOperatingOrUndefinedActivities(CashFlowActivity activity)
    {
        Assert.Throws<DomainException>(() => Create(activity));
    }

    [Fact]
    public void Create_NormalizesCurrencyAndCategory_AndAllowsNoMemo()
    {
        var disclosure = CashFlowNonCashDisclosure.Create(
            Guid.NewGuid(), BookId, EntryId, 1, 10m, " eur ",
            CashFlowActivity.Investing, "  PPE_ACQUIRED_NONCASH  ", null,
            ActorId, Now);

        Assert.Equal("EUR", disclosure.Currency);
        Assert.Equal("PPE_ACQUIRED_NONCASH", disclosure.Category);
        Assert.Null(disclosure.Memo);
    }

    [Fact]
    public void Create_RejectsInvalidAmountPrecisionAndIdentifiers()
    {
        Assert.Throws<DomainException>(() => CashFlowNonCashDisclosure.Create(
            Guid.NewGuid(), BookId, EntryId, 1, 0m, "USD",
            CashFlowActivity.Investing, "PPE", null, ActorId, Now));

        Assert.Throws<DomainException>(() => CashFlowNonCashDisclosure.Create(
            Guid.NewGuid(), BookId, EntryId, 1, 10.12345m, "USD",
            CashFlowActivity.Investing, "PPE", null, ActorId, Now));

        Assert.Throws<ArgumentOutOfRangeException>(() => CashFlowNonCashDisclosure.Create(
            Guid.NewGuid(), BookId, EntryId, 0, 10m, "USD",
            CashFlowActivity.Investing, "PPE", null, ActorId, Now));

        Assert.Throws<ArgumentException>(() => CashFlowNonCashDisclosure.Create(
            Guid.Empty, BookId, EntryId, 1, 10m, "USD",
            CashFlowActivity.Investing, "PPE", null, ActorId, Now));
    }

    [Theory]
    [InlineData("BAD-CATEGORY")]
    [InlineData("1STARTS_WITH_NUMBER")]
    [InlineData("HAS SPACE")]
    public void Create_RejectsUnstableCategoryCodes(string category)
    {
        Assert.Throws<DomainException>(() => CashFlowNonCashDisclosure.Create(
            Guid.NewGuid(), BookId, EntryId, 1, 10m, "USD",
            CashFlowActivity.Investing, category, null, ActorId, Now));
    }

    private static CashFlowNonCashDisclosure Create(CashFlowActivity activity) =>
        CashFlowNonCashDisclosure.Create(
            Guid.NewGuid(), BookId, EntryId, 2, 125m, "usd",
            activity, "ASSET_ACQUIRED_THROUGH_LEASE", "Lease commencement",
            ActorId, Now);
}
