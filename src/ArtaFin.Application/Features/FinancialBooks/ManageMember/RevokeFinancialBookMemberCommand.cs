namespace ArtaFin.Application.Features.FinancialBooks.ManageMember;

public sealed record RevokeFinancialBookMemberCommand(
    Guid FinancialBookId,
    Guid MembershipId);
