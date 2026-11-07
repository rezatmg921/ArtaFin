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
/// A ledger account owned by one financial book.
/// Its balance is derived from posted journal entries and is never stored here.
/// </summary>
public sealed class Account : AggregateRoot
{
    public Guid FinancialBookId { get; }
    public string Code { get; }
    public string Name { get; private set; }
    public AccountType Type { get; }
    public AccountNormalBalance NormalBalance { get; }
    public AccountStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? DeactivatedAt { get; private set; }

    private Account(
        Guid id,
        Guid financialBookId,
        string code,
        string name,
        AccountType type,
        AccountNormalBalance normalBalance,
        DateTimeOffset createdAt) : base(id)
    {
        FinancialBookId = financialBookId;
        Code = code;
        Name = name;
        Type = type;
        NormalBalance = normalBalance;
        CreatedAt = createdAt;
        Status = AccountStatus.Active;
    }

    public static Account Create(
        Guid id,
        Guid financialBookId,
        string code,
        string name,
        AccountType type,
        DateTimeOffset createdAt)
    {
        if (financialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(financialBookId));
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Account code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Account name is required.", nameof(name));
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type));

        var normalizedCode = code.Trim().ToUpperInvariant();
        var normalizedName = name.Trim();
        var normalBalance = type switch
        {
            AccountType.Asset or AccountType.Expense => AccountNormalBalance.Debit,
            AccountType.Liability or AccountType.Equity or AccountType.Income => AccountNormalBalance.Credit,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        return new Account(id, financialBookId, normalizedCode, normalizedName,
            type, normalBalance, createdAt);
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Account name is required.", nameof(name));

        Name = name.Trim();
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
