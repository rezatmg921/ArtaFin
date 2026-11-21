using ArtaFin.Domain.Common;

namespace ArtaFin.Domain.Finance;

public enum AccountType
{
    Asset = 1,
    Liability = 2,
    Equity = 3,
    Income = 4,
    Expense = 5
}

public enum AccountNormalBalance
{
    Debit = 1,
    Credit = 2
}

public enum AccountStatus
{
    Active = 1,
    Inactive = 2
}

/// <summary>
/// Operational policy controlling whether posting may make this account's
/// normal-balance amount negative. It is independent of accounting type.
/// </summary>
public enum AccountBalancePolicy
{
    PreventNegative = 1,
    AllowNegative = 2
}

/// <summary>
/// Identifies accounts included in cash and cash equivalents for IAS 7 reporting.
/// Cash equivalents require separately evidenced qualifying characteristics.
/// </summary>
public enum CashFlowAccountType
{
    None = 0,
    Cash = 1,
    CashEquivalent = 2
}

/// <summary>
/// Explicitly identifies liabilities arising from financing activities for IAS 7.44A.
/// Ordinary trade and operating liabilities remain None unless specifically designated.
/// </summary>
public enum FinancingLiabilityType : byte
{
    None = 0,
    Borrowings = 1,
    LeaseLiabilities = 2,
    OtherFinancingLiabilities = 3
}

/// <summary>
/// A ledger account owned by one financial book.
/// Its balance is derived from posted journal entries and is never stored here.
/// </summary>
public sealed class Account : AggregateRoot
{
    public const int MaxCodeLength = 50;
    public const int MaxNameLength = 200;

    public Guid FinancialBookId { get; }
    public string Code { get; }
    public string Name { get; private set; }
    public AccountType Type { get; }
    public AccountNormalBalance NormalBalance { get; }
    public AccountBalancePolicy BalancePolicy { get; private set; }
    public AccountStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? DeactivatedAt { get; private set; }
    public CashFlowAccountType CashFlowAccountType { get; private set; }
    public int? CashEquivalentMaturityDays { get; private set; }
    public bool? CashEquivalentRiskInsignificant { get; private set; }
    public FinancingLiabilityType FinancingLiabilityType { get; private set; }

    private Account(
        Guid id,
        Guid financialBookId,
        string code,
        string name,
        AccountType type,
        AccountNormalBalance normalBalance,
        AccountBalancePolicy balancePolicy,
        DateTimeOffset createdAt) : base(id)
    {
        FinancialBookId = financialBookId;
        Code = code;
        Name = name;
        Type = type;
        NormalBalance = normalBalance;
        BalancePolicy = balancePolicy;
        CreatedAt = createdAt;
        Status = AccountStatus.Active;
    }

    public static Account Create(
        Guid id,
        Guid financialBookId,
        string code,
        string name,
        AccountType type,
        DateTimeOffset createdAt,
        AccountBalancePolicy? balancePolicy = null)
    {
        if (financialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(financialBookId));
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Account code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Account name is required.", nameof(name));

        var normalizedCode = code.Trim().ToUpperInvariant();
        var normalizedName = name.Trim();
        if (normalizedCode.Length > MaxCodeLength)
            throw new ArgumentException($"Account code cannot exceed {MaxCodeLength} characters.", nameof(code));
        if (normalizedName.Length > MaxNameLength)
            throw new ArgumentException($"Account name cannot exceed {MaxNameLength} characters.", nameof(name));
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type));

        var normalBalance = type switch
        {
            AccountType.Asset or AccountType.Expense => AccountNormalBalance.Debit,
            AccountType.Liability or AccountType.Equity or AccountType.Income => AccountNormalBalance.Credit,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        var effectiveBalancePolicy = balancePolicy ?? (type == AccountType.Asset
            ? AccountBalancePolicy.PreventNegative
            : AccountBalancePolicy.AllowNegative);

        if (!Enum.IsDefined(effectiveBalancePolicy))
            throw new ArgumentOutOfRangeException(nameof(balancePolicy));

        return new Account(id, financialBookId, normalizedCode, normalizedName,
            type, normalBalance, effectiveBalancePolicy, createdAt);
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Account name is required.", nameof(name));

        var normalizedName = name.Trim();
        if (normalizedName.Length > MaxNameLength)
            throw new ArgumentException($"Account name cannot exceed {MaxNameLength} characters.", nameof(name));

        Name = normalizedName;
    }

    public void ChangeBalancePolicy(AccountBalancePolicy balancePolicy)
    {
        if (!Enum.IsDefined(balancePolicy))
            throw new ArgumentOutOfRangeException(nameof(balancePolicy));

        BalancePolicy = balancePolicy;
    }

    /// <summary>
    /// Configures whether this asset account forms part of cash and cash equivalents.
    /// A cash equivalent must have an acquisition maturity of at most 90 days and
    /// an explicitly confirmed insignificant risk of changes in value.
    /// </summary>
    public void ConfigureCashFlowClassification(
        CashFlowAccountType accountType,
        int? cashEquivalentMaturityDays = null,
        bool? cashEquivalentRiskInsignificant = null)
    {
        if (!Enum.IsDefined(accountType))
            throw new ArgumentOutOfRangeException(nameof(accountType));

        if (accountType != CashFlowAccountType.None && Type != AccountType.Asset)
            throw new DomainException("Only asset accounts can be classified as cash or cash equivalents.");

        if (accountType == CashFlowAccountType.Cash)
        {
            if (cashEquivalentMaturityDays.HasValue || cashEquivalentRiskInsignificant.HasValue)
                throw new DomainException("Cash accounts must not contain cash-equivalent assessment fields.");
        }
        else if (accountType == CashFlowAccountType.CashEquivalent)
        {
            if (!cashEquivalentMaturityDays.HasValue ||
                cashEquivalentMaturityDays.Value is < 0 or > 90)
                throw new DomainException("Cash equivalents must have an acquisition maturity between 0 and 90 days.");
            if (cashEquivalentRiskInsignificant != true)
                throw new DomainException("Cash equivalents require confirmation that the risk of changes in value is insignificant.");
        }
        else if (cashEquivalentMaturityDays.HasValue || cashEquivalentRiskInsignificant.HasValue)
        {
            throw new DomainException("Accounts not classified as cash equivalents must not contain cash-equivalent assessment fields.");
        }

        CashFlowAccountType = accountType;
        CashEquivalentMaturityDays = cashEquivalentMaturityDays;
        CashEquivalentRiskInsignificant = cashEquivalentRiskInsignificant;
    }

    /// <summary>
    /// Explicitly scopes this liability account into the IAS 7.44A financing-liability reconciliation.
    /// </summary>
    public void ConfigureFinancingLiabilityClassification(FinancingLiabilityType liabilityType)
    {
        if (!Enum.IsDefined(liabilityType))
            throw new ArgumentOutOfRangeException(nameof(liabilityType));
        if (liabilityType != FinancingLiabilityType.None && Type != AccountType.Liability)
            throw new DomainException("Only liability accounts can be classified as financing liabilities.");

        FinancingLiabilityType = liabilityType;
    }

    public void Deactivate(DateTimeOffset deactivatedAt)
    {
        if (Status == AccountStatus.Inactive)
            throw new DomainException("Account is already inactive.");
        if (deactivatedAt < CreatedAt)
            throw new DomainException("Account cannot be deactivated before it was created.");

        Status = AccountStatus.Inactive;
        DeactivatedAt = deactivatedAt;
    }

    public void Reactivate(DateTimeOffset reactivatedAt)
    {
        if (Status != AccountStatus.Inactive || DeactivatedAt is null)
            throw new DomainException("Only an inactive account can be reactivated.");
        if (reactivatedAt < DeactivatedAt.Value)
            throw new DomainException("Account cannot be reactivated before deactivation.");

        Status = AccountStatus.Active;
        DeactivatedAt = null;
    }
}
