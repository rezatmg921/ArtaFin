using ArtaFin.Domain.Finance;
using MediatR;

namespace ArtaFin.Application.Features.Transfers.CreateTransfer;

/// <summary>Transfers value between two asset accounts by posting a balanced journal entry.</summary>
public sealed record CreateTransferCommand(
    Guid FinancialBookId,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    string Description,
    Money Amount,
    string? IdempotencyKey = null) : IRequest<Guid>;
