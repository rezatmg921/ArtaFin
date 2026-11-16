using ArtaFin.Domain.Finance;
using MediatR;

namespace ArtaFin.Application.Features.Income.CreateIncome;

/// <summary>Records income received into an asset account by posting a balanced journal entry.</summary>
public sealed record CreateIncomeCommand(
    Guid FinancialBookId,
    Guid IncomeAccountId,
    Guid DepositAccountId,
    string Description,
    Money Amount,
    string? IdempotencyKey = null) : IRequest<Guid>;
