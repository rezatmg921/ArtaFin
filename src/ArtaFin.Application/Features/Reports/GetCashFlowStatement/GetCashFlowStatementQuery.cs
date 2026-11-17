using MediatR;

namespace ArtaFin.Application.Features.Reports.GetCashFlowStatement;

public sealed record GetCashFlowStatementQuery(
    Guid FinancialBookId,
    DateTimeOffset From,
    DateTimeOffset To) : IRequest<CashFlowStatementResult>;
