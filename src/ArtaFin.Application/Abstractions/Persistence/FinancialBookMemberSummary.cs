using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Abstractions.Persistence;

public sealed record FinancialBookMemberSummary(
    Guid MembershipId,
    Guid UserId,
    string Email,
    FinancialBookRole Role,
    DateTimeOffset GrantedAt);
