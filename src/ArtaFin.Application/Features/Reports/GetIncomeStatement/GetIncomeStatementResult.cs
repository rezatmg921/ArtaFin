using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Features.Reports.GetIncomeStatement;

public sealed record IncomeStatementAccountResult(
    Guid AccountId,
    string Code,
    string Name,
    AccountType Type,
    decimal Amount);

public sealed record IncomeStatementResult(
    DateTimeOffset From,
    DateTimeOffset To,
    string Currency,
    IReadOnlyList<IncomeStatementAccountResult> IncomeAccounts,
    IReadOnlyList<IncomeStatementAccountResult> ExpenseAccounts,
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal NetProfit);
