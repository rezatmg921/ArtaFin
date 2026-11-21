using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Abstractions.Persistence;

public interface ILeasePaymentScheduleRepository
{
    Task<LeasePaymentSchedule?> GetByIdAsync(
        Guid financialBookId, Guid leaseContractId, Guid scheduleId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LeasePaymentSchedule>> GetByContractAsync(
        Guid financialBookId, Guid leaseContractId, CancellationToken cancellationToken);
    Task<LeasePaymentSchedule?> GetLatestAsync(
        Guid financialBookId, Guid leaseContractId, CancellationToken cancellationToken);
    Task<LeasePaymentSchedule?> GetLatestFinalizedAsync(
        Guid financialBookId, Guid leaseContractId, CancellationToken cancellationToken);
    Task AddAsync(LeasePaymentSchedule schedule, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
