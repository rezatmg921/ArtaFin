using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Features.FinancialBooks.InviteMember;

public sealed record InviteFinancialBookMemberCommand(
    Guid FinancialBookId,
    string Email,
    FinancialBookRole Role);
