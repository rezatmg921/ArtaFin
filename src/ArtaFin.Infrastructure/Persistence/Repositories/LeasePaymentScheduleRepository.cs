using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Infrastructure.Persistence.Repositories;

internal sealed class LeasePaymentScheduleRepository(ArtaFinDbContext dbContext)
    : ILeasePaymentScheduleRepository
{
    public Task<LeasePaymentSchedule?> GetByIdAsync(
        Guid financialBookId,
        Guid leaseContractId,
        Guid scheduleId,
        CancellationToken cancellationToken) =>
        dbContext.LeasePaymentSchedules
            .Include(schedule => schedule.Payments)
            .SingleOrDefaultAsync(schedule =>
                schedule.FinancialBookId == financialBookId &&
                schedule.LeaseContractId == leaseContractId &&
                schedule.Id == scheduleId,
                cancellationToken);

    public async Task<IReadOnlyList<LeasePaymentSchedule>> GetByContractAsync(
        Guid financialBookId,
        Guid leaseContractId,
        CancellationToken cancellationToken) =>
        await dbContext.LeasePaymentSchedules
            .Include(schedule => schedule.Payments)
            .AsNoTracking()
            .Where(schedule =>
                schedule.FinancialBookId == financialBookId &&
                schedule.LeaseContractId == leaseContractId)
            .OrderByDescending(schedule => schedule.Version)
            .ToListAsync(cancellationToken);

    public Task<LeasePaymentSchedule?> GetLatestAsync(
        Guid financialBookId,
        Guid leaseContractId,
        CancellationToken cancellationToken) =>
        dbContext.LeasePaymentSchedules
            .Include(schedule => schedule.Payments)
            .Where(schedule =>
                schedule.FinancialBookId == financialBookId &&
                schedule.LeaseContractId == leaseContractId)
            .OrderByDescending(schedule => schedule.Version)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<LeasePaymentSchedule?> GetLatestFinalizedAsync(
        Guid financialBookId,
        Guid leaseContractId,
        CancellationToken cancellationToken) =>
        dbContext.LeasePaymentSchedules
            .Where(schedule =>
                schedule.FinancialBookId == financialBookId &&
                schedule.LeaseContractId == leaseContractId &&
                schedule.Status == LeasePaymentScheduleStatus.Finalized)
            .OrderByDescending(schedule => schedule.Version)
            .FirstOrDefaultAsync(cancellationToken);

    public Task AddAsync(LeasePaymentSchedule schedule, CancellationToken cancellationToken) =>
        dbContext.LeasePaymentSchedules.AddAsync(schedule, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
