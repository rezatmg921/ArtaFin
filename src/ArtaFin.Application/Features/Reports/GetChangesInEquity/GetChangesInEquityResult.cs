using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Features.Reports.GetChangesInEquity;

public sealed record ChangesInEquityAccountResult(
    Guid AccountId,
    string Code,
    string Name,
    decimal OpeningBalance,
    decimal Increases,
    decimal Decreases,
    decimal ClosingBalance,
    decimal OwnerContributions,
    decimal OwnerDistributions,
    decimal TransfersIn,
    decimal TransfersOut,
    decimal OtherIncreases,
    decimal OtherDecreases,
    decimal UnclassifiedIncreases,
    decimal UnclassifiedDecreases);

public sealed record ChangesInEquityResult(
    DateTimeOffset From,
    DateTimeOffset To,
    string Currency,
    IReadOnlyList<ChangesInEquityAccountResult> Components,
    decimal OpeningEquity,
    decimal TotalIncreases,
    decimal TotalDecreases,
    decimal ClosingEquity,
    decimal OpeningUnclosedNetIncome,
    decimal ClosingUnclosedNetIncome,
    decimal OpeningTotalEquity,
    decimal ClosingTotalEquity);
