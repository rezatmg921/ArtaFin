using ArtaFin.Domain.Common;

namespace ArtaFin.Domain.Finance;

/// <summary>
/// A user's membership in one financial book.
/// The owner is not represented by a membership record.
/// </summary>
public sealed class FinancialBookMember : Entity
{
    public Guid FinancialBookId { get; }
    public Guid UserId { get; }
    public FinancialBookRole Role { get; private set; }
    public DateTimeOffset GrantedAt { get; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private FinancialBookMember(
        Guid id,
        Guid financialBookId,
        Guid userId,
        FinancialBookRole role,
        DateTimeOffset grantedAt) : base(id)
    {
        FinancialBookId = financialBookId;
        UserId = userId;
        Role = role;
        GrantedAt = grantedAt;
    }

    public static FinancialBookMember Grant(
        Guid id,
        Guid financialBookId,
        Guid userId,
        FinancialBookRole role,
        DateTimeOffset grantedAt)
    {
        if (financialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(financialBookId));
        if (userId == Guid.Empty)
            throw new ArgumentException("User id cannot be empty.", nameof(userId));
        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role));

        return new FinancialBookMember(id, financialBookId, userId, role, grantedAt);
    }

    public bool IsActiveAt(DateTimeOffset at) =>
        GrantedAt <= at && (RevokedAt is null || RevokedAt > at);

    public bool CanCreateAccountsAt(DateTimeOffset at) =>
        IsActiveAt(at) && Role == FinancialBookRole.Accountant;

    public bool CanPostJournalEntriesAt(DateTimeOffset at) =>
        IsActiveAt(at) && Role == FinancialBookRole.Accountant;

    public void ChangeRole(FinancialBookRole role)
    {
        if (RevokedAt is not null)
            throw new DomainException("A revoked membership cannot be changed.");
        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role));

        Role = role;
    }

    public void Revoke(DateTimeOffset revokedAt)
    {
        if (RevokedAt is not null)
            throw new DomainException("Membership is already revoked.");
        if (revokedAt < GrantedAt)
            throw new DomainException("Membership cannot be revoked before it was granted.");

        RevokedAt = revokedAt;
    }
}
