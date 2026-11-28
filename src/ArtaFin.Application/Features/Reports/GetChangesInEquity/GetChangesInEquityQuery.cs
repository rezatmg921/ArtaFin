using MediatR;

namespace ArtaFin.Application.Features.Reports.GetChangesInEquity;

public sealed record GetChangesInEquityQuery(
    Guid FinancialBookId,
    DateTimeOffset From,
    DateTimeOffset To) : IRequest<ChangesInEquityResult>;
