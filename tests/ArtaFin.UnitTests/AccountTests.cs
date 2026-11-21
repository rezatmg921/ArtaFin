using ArtaFin.Domain.Common;
using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests;

public sealed class AccountTests
{
    private static readonly Guid BookId = Guid.NewGuid();
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 7, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_NormalizesCodeAndName_AndStartsActive()
    {
        var account = Account.Create(
            Guid.NewGuid(), BookId, "  bank-01 ", "  Main Bank  ",
            AccountType.Asset, CreatedAt);

        Assert.Equal("BANK-01", account.Code);
        Assert.Equal("Main Bank", account.Name);
        Assert.Equal(AccountStatus.Active, account.Status);
        Assert.Equal(AccountNormalBalance.Debit, account.NormalBalance);
        Assert.Equal(BookId, account.FinancialBookId);
        Assert.Null(account.DeactivatedAt);
    }

    [Theory]
    [InlineData(AccountType.Asset, AccountNormalBalance.Debit)]
    [InlineData(AccountType.Expense, AccountNormalBalance.Debit)]
    [InlineData(AccountType.Liability, AccountNormalBalance.Credit)]
    [InlineData(AccountType.Equity, AccountNormalBalance.Credit)]
    [InlineData(AccountType.Income, AccountNormalBalance.Credit)]
    public void NormalBalance_FollowsInitialAccountingConvention(
        AccountType type, AccountNormalBalance expected)
    {
        var account = Account.Create(
            Guid.NewGuid(), BookId, "1000", "Test", type, CreatedAt);

        Assert.Equal(expected, account.NormalBalance);
    }

    [Fact]
    public void Create_RejectsEmptyFinancialBookId()
    {
        Assert.Throws<ArgumentException>(() => Account.Create(
            Guid.NewGuid(), Guid.Empty, "1000", "Bank", AccountType.Asset, CreatedAt));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsMissingCode(string code)
    {
        Assert.Throws<ArgumentException>(() => Account.Create(
            Guid.NewGuid(), BookId, code, "Bank", AccountType.Asset, CreatedAt));
    }

    [Fact]
    public void Create_RejectsMissingName()
    {
        Assert.Throws<ArgumentException>(() => Account.Create(
            Guid.NewGuid(), BookId, "1000", " ", AccountType.Asset, CreatedAt));
    }

    [Fact]
    public void Create_RejectsUnknownAccountType()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Account.Create(
            Guid.NewGuid(), BookId, "1000", "Bank", (AccountType)999, CreatedAt));
    }

    [Fact]
    public void Rename_TrimsName()
    {
        var account = CreateAccount();

        account.Rename("  Operating Bank  ");

        Assert.Equal("Operating Bank", account.Name);
    }

    [Fact]
    public void Rename_RejectsBlankName()
    {
        var account = CreateAccount();

        Assert.Throws<ArgumentException>(() => account.Rename(" "));
    }

    [Fact]
    public void Deactivate_ChangesStatusAndRecordsTimestamp()
    {
        var account = CreateAccount();
        var deactivatedAt = CreatedAt.AddDays(1);

        account.Deactivate(deactivatedAt);

        Assert.Equal(AccountStatus.Inactive, account.Status);
        Assert.Equal(deactivatedAt, account.DeactivatedAt);
    }

    [Fact]
    public void Deactivate_RejectsEarlierTimestamp()
    {
        var account = CreateAccount();

        Assert.Throws<DomainException>(() => account.Deactivate(CreatedAt.AddTicks(-1)));
    }

    [Fact]
    public void Deactivate_RejectsAlreadyInactiveAccount()
    {
        var account = CreateAccount();
        account.Deactivate(CreatedAt.AddDays(1));

        Assert.Throws<DomainException>(() => account.Deactivate(CreatedAt.AddDays(2)));
    }

    [Fact]
    public void Reactivate_RequiresInactiveAccountAndLaterTimestamp()
    {
        var account = CreateAccount();
        var deactivatedAt = CreatedAt.AddDays(1);
        account.Deactivate(deactivatedAt);

        account.Reactivate(deactivatedAt.AddDays(1));

        Assert.Equal(AccountStatus.Active, account.Status);
        Assert.Null(account.DeactivatedAt);
    }

    [Fact]
    public void Reactivate_RejectsActiveAccount()
    {
        var account = CreateAccount();

        Assert.Throws<DomainException>(() => account.Reactivate(CreatedAt.AddDays(1)));
    }

    [Fact]
    public void ConfigureCashFlowClassification_AllowsCashAsset()
    {
        var account = CreateAccount();

        account.ConfigureCashFlowClassification(CashFlowAccountType.Cash);

        Assert.Equal(CashFlowAccountType.Cash, account.CashFlowAccountType);
        Assert.Null(account.CashEquivalentMaturityDays);
        Assert.Null(account.CashEquivalentRiskInsignificant);
    }

    [Fact]
    public void ConfigureCashFlowClassification_RequiresQualifyingCashEquivalentEvidence()
    {
        var account = CreateAccount();

        Assert.Throws<DomainException>(() =>
            account.ConfigureCashFlowClassification(CashFlowAccountType.CashEquivalent, 91, true));
        Assert.Throws<DomainException>(() =>
            account.ConfigureCashFlowClassification(CashFlowAccountType.CashEquivalent, 30, false));

        account.ConfigureCashFlowClassification(CashFlowAccountType.CashEquivalent, 30, true);

        Assert.Equal(CashFlowAccountType.CashEquivalent, account.CashFlowAccountType);
        Assert.Equal(30, account.CashEquivalentMaturityDays);
        Assert.True(account.CashEquivalentRiskInsignificant);
    }

    [Fact]
    public void ConfigureCashFlowClassification_RejectsNonAssetAccounts()
    {
        var income = Account.Create(
            Guid.NewGuid(), BookId, "4000", "Service income", AccountType.Income, CreatedAt);

        Assert.Throws<DomainException>(() =>
            income.ConfigureCashFlowClassification(CashFlowAccountType.Cash));
    }

    [Fact]
    public void ConfigureCashFlowClassification_CanRemoveClassification()
    {
        var account = CreateAccount();
        account.ConfigureCashFlowClassification(CashFlowAccountType.CashEquivalent, 30, true);

        account.ConfigureCashFlowClassification(CashFlowAccountType.None);

        Assert.Equal(CashFlowAccountType.None, account.CashFlowAccountType);
        Assert.Null(account.CashEquivalentMaturityDays);
        Assert.Null(account.CashEquivalentRiskInsignificant);
    }

    private static Account CreateAccount() => Account.Create(
        Guid.NewGuid(), BookId, "1000", "Main Bank", AccountType.Asset, CreatedAt);
    [Fact]
    public void Create_DefaultsAssetToPreventNegativeAndNonAssetToAllowNegative()
    {
        var asset = Account.Create(Guid.NewGuid(), BookId, "1000", "Cash", AccountType.Asset, CreatedAt);
        var income = Account.Create(Guid.NewGuid(), BookId, "4000", "Income", AccountType.Income, CreatedAt);

        Assert.Equal(AccountBalancePolicy.PreventNegative, asset.BalancePolicy);
        Assert.Equal(AccountBalancePolicy.AllowNegative, income.BalancePolicy);
    }

    [Fact]
    public void Create_AllowsExplicitBalancePolicyOverride()
    {
        var overdraftBank = Account.Create(
            Guid.NewGuid(), BookId, "1010", "Overdraft bank", AccountType.Asset, CreatedAt,
            AccountBalancePolicy.AllowNegative);

        Assert.Equal(AccountBalancePolicy.AllowNegative, overdraftBank.BalancePolicy);
    }

    [Fact]
    public void ChangeBalancePolicy_ChangesOperationalPolicyWithoutChangingAccountType()
    {
        var account = CreateAccount();

        account.ChangeBalancePolicy(AccountBalancePolicy.AllowNegative);

        Assert.Equal(AccountType.Asset, account.Type);
        Assert.Equal(AccountNormalBalance.Debit, account.NormalBalance);
        Assert.Equal(AccountBalancePolicy.AllowNegative, account.BalancePolicy);
    }

}
