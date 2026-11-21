using ArtaFin.Domain.Common;
using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests.Domain.Finance;

public sealed class FinancialBookMemberTests
{
    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid BookId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset GrantedAt =
        new(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);

    private static FinancialBookMember CreateMember(
        FinancialBookRole role = FinancialBookRole.Accountant) =>
        FinancialBookMember.Grant(
            MemberId, BookId, UserId, role, GrantedAt);

    [Fact]
    public void Grant_CreatesMembershipWithSuppliedValues()
    {
        var member = CreateMember(FinancialBookRole.Auditor);

        Assert.Equal(MemberId, member.Id);
        Assert.Equal(BookId, member.FinancialBookId);
        Assert.Equal(UserId, member.UserId);
        Assert.Equal(FinancialBookRole.Auditor, member.Role);
        Assert.Equal(GrantedAt, member.GrantedAt);
        Assert.Null(member.RevokedAt);
    }

    [Fact]
    public void Grant_RejectsEmptyEntityId()
    {
        Assert.Throws<ArgumentException>(() =>
            FinancialBookMember.Grant(
                Guid.Empty, BookId, UserId,
                FinancialBookRole.Accountant, GrantedAt));
    }

    [Fact]
    public void Grant_RejectsEmptyFinancialBookId()
    {
        Assert.Throws<ArgumentException>(() =>
            FinancialBookMember.Grant(
                Guid.NewGuid(), Guid.Empty, UserId,
                FinancialBookRole.Accountant, GrantedAt));
    }

    [Fact]
    public void Grant_RejectsEmptyUserId()
    {
        Assert.Throws<ArgumentException>(() =>
            FinancialBookMember.Grant(
                Guid.NewGuid(), BookId, Guid.Empty,
                FinancialBookRole.Accountant, GrantedAt));
    }

    [Fact]
    public void Grant_RejectsUndefinedRole()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FinancialBookMember.Grant(
                Guid.NewGuid(), BookId, UserId,
                (FinancialBookRole)999, GrantedAt));
    }

    [Fact]
    public void IsActiveAt_ReturnsFalseBeforeMembershipWasGranted()
    {
        var member = CreateMember();

        Assert.False(member.IsActiveAt(GrantedAt.AddTicks(-1)));
    }

    [Fact]
    public void IsActiveAt_ReturnsTrueAtGrantTime()
    {
        var member = CreateMember();

        Assert.True(member.IsActiveAt(GrantedAt));
    }

    [Fact]
    public void IsActiveAt_ReturnsTrueBeforeRevocationTime()
    {
        var member = CreateMember();
        member.Revoke(GrantedAt.AddHours(2));

        Assert.True(member.IsActiveAt(GrantedAt.AddHours(1)));
    }

    [Fact]
    public void IsActiveAt_ReturnsFalseAtAndAfterRevocationTime()
    {
        var member = CreateMember();
        var revokedAt = GrantedAt.AddHours(2);
        member.Revoke(revokedAt);

        Assert.False(member.IsActiveAt(revokedAt));
        Assert.False(member.IsActiveAt(revokedAt.AddTicks(1)));
    }

    [Theory]
    [InlineData(FinancialBookRole.Accountant, true)]
    [InlineData(FinancialBookRole.Auditor, false)]
    [InlineData(FinancialBookRole.Viewer, false)]
    public void CanCreateAccountsAt_RequiresActiveAccountantMembership(
        FinancialBookRole role, bool expected)
    {
        var member = FinancialBookMember.Grant(
            Guid.NewGuid(), BookId, UserId, role, GrantedAt);

        Assert.Equal(expected, member.CanCreateAccountsAt(GrantedAt));
    }

    [Theory]
    [InlineData(FinancialBookRole.Accountant, true)]
    [InlineData(FinancialBookRole.Auditor, false)]
    [InlineData(FinancialBookRole.Viewer, false)]
    public void CanPostJournalEntriesAt_RequiresActiveAccountantMembership(
        FinancialBookRole role, bool expected)
    {
        var member = FinancialBookMember.Grant(
            Guid.NewGuid(), BookId, UserId, role, GrantedAt);

        Assert.Equal(expected, member.CanPostJournalEntriesAt(GrantedAt));
    }

    [Fact]
    public void CanCreateAccountsAt_ReturnsFalseAfterRevocation()
    {
        var member = CreateMember();
        member.Revoke(GrantedAt.AddHours(1));

        Assert.False(member.CanCreateAccountsAt(GrantedAt.AddHours(1)));
    }

    [Fact]
    public void CanPostJournalEntriesAt_ReturnsFalseAfterRevocation()
    {
        var member = CreateMember();
        member.Revoke(GrantedAt.AddHours(1));

        Assert.False(member.CanPostJournalEntriesAt(GrantedAt.AddHours(1)));
    }

    [Fact]
    public void ChangeRole_UpdatesRoleForActiveMembership()
    {
        var member = CreateMember();

        member.ChangeRole(FinancialBookRole.Auditor);

        Assert.Equal(FinancialBookRole.Auditor, member.Role);
    }

    [Fact]
    public void ChangeRole_RejectsUndefinedRole()
    {
        var member = CreateMember();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            member.ChangeRole((FinancialBookRole)999));
    }

    [Fact]
    public void ChangeRole_RejectsRevokedMembership()
    {
        var member = CreateMember();
        member.Revoke(GrantedAt.AddHours(1));

        Assert.Throws<DomainException>(() =>
            member.ChangeRole(FinancialBookRole.Auditor));
    }

    [Fact]
    public void Revoke_SetsRevocationTimestamp()
    {
        var member = CreateMember();
        var revokedAt = GrantedAt.AddHours(1);

        member.Revoke(revokedAt);

        Assert.Equal(revokedAt, member.RevokedAt);
    }

    [Fact]
    public void Revoke_AllowsRevocationAtGrantTime()
    {
        var member = CreateMember();

        member.Revoke(GrantedAt);

        Assert.Equal(GrantedAt, member.RevokedAt);
        Assert.False(member.IsActiveAt(GrantedAt));
    }

    [Fact]
    public void Revoke_RejectsRevocationBeforeGrantTime()
    {
        var member = CreateMember();

        Assert.Throws<DomainException>(() =>
            member.Revoke(GrantedAt.AddTicks(-1)));
        Assert.Null(member.RevokedAt);
    }

    [Fact]
    public void Revoke_RejectsSecondRevocation()
    {
        var member = CreateMember();
        member.Revoke(GrantedAt.AddHours(1));

        Assert.Throws<DomainException>(() =>
            member.Revoke(GrantedAt.AddHours(2)));
    }
}
