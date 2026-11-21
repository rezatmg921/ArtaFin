using ArtaFin.Domain.Finance;
using MediatR;

namespace ArtaFin.Application.Features.Expenses.CreateExpense;

/// <summary>
/// Records an expense paid from an asset account by posting a balanced journal entry.
/// </summary>
public sealed record CreateExpenseCommand(
    Guid FinancialBookId,
    Guid ExpenseAccountId,
    Guid PaymentAccountId,
    string Description,
    Money Amount,
    string? IdempotencyKey = null) : IRequest<Guid>;
