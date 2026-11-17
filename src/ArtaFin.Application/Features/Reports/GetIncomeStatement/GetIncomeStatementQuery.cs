using MediatR;

namespace ArtaFin.Application.Features.Reports.GetIncomeStatement;

public sealed record GetIncomeStatementQuery(
    Guid FinancialBookId,
    DateTimeOffset From,
    DateTimeOffset To) : IRequest<IncomeStatementResult>;
