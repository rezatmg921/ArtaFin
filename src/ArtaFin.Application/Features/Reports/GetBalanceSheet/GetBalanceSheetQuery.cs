using MediatR;

namespace ArtaFin.Application.Features.Reports.GetBalanceSheet;

public sealed record GetBalanceSheetQuery(
    Guid FinancialBookId,
    DateTimeOffset? AsOf) : IRequest<BalanceSheetResult>;
