using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Features.FinancialBooks.ManageMember;

public sealed record ChangeFinancialBookMemberRoleCommand(
    Guid FinancialBookId,
    Guid MembershipId,
    FinancialBookRole Role);
