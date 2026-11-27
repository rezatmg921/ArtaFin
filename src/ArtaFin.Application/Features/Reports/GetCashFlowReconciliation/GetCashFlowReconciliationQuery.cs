using MediatR;

namespace ArtaFin.Application.Features.Reports.GetCashFlowReconciliation;

public sealed record GetCashFlowReconciliationQuery(
    Guid FinancialBookId,
    DateTimeOffset From,
    DateTimeOffset To) : IRequest<CashFlowReconciliationResult>;
