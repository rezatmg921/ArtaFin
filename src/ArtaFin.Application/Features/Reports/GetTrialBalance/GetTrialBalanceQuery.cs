using MediatR;

namespace ArtaFin.Application.Features.Reports.GetTrialBalance;

public sealed record GetTrialBalanceQuery(
    Guid FinancialBookId,
    DateTimeOffset? AsOf) : IRequest<TrialBalanceResult>;
