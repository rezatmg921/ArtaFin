using ArtaFin.Domain.Obligations;

namespace ArtaFin.Application.Abstractions.Persistence;

public interface IObligationMaturityReadRepository
{
    Task<IReadOnlyList<ObligationMaturitySnapshot>> GetAsOfAsync(
        Guid financialBookId, DateTimeOffset asOf, CancellationToken cancellationToken);
}

public sealed record ObligationMaturitySnapshot(
    Guid ObligationId,
    Guid ControlAccountId,
    FinancialObligationDirection Direction,
    string Currency,
    IReadOnlyList<ObligationInstallmentTerm> Installments,
    IReadOnlyList<ObligationInstallmentPayment> Payments);
