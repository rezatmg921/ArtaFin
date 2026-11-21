using MediatR;

namespace ArtaFin.Application.Features.Reports.GetFinancingLiabilitiesReconciliation;

public sealed record GetFinancingLiabilitiesReconciliationQuery(
    Guid FinancialBookId,
    DateTimeOffset From,
    DateTimeOffset To) : IRequest<FinancingLiabilitiesReconciliationResult>;
