using ArtaFin.Domain.Finance;
using ArtaFin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArtaFin.UnitTests.Finance;

public sealed class LeasePaymentSchedulePersistenceModelTests
{
    [Fact]
    public void Model_MapsVersionedSchedulesAndDatedPaymentRows()
    {
        var options = new DbContextOptionsBuilder<ArtaFinDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ArtaFinLeaseScheduleModelOnly;Trusted_Connection=True;")
            .Options;

        using var context = new ArtaFinDbContext(options);
        var schedule = context.Model.FindEntityType(typeof(LeasePaymentSchedule));
        var payment = context.Model.FindEntityType(typeof(LeasePaymentScheduleLine));
        var contract = context.Model.FindEntityType(typeof(LeaseContract));

        Assert.NotNull(schedule);
        Assert.NotNull(payment);
        Assert.NotNull(contract);
        Assert.Equal("LeasePaymentSchedules", schedule!.GetTableName());
        Assert.Equal("LeasePaymentScheduleLines", payment!.GetTableName());

        Assert.Contains(schedule.GetIndexes(), index =>
            index.IsUnique &&
            index.GetDatabaseName() == "UX_LeasePaymentSchedules_Contract_Version");

        Assert.Contains(payment.GetIndexes(), index =>
            index.IsUnique &&
            index.GetDatabaseName() == "UX_LeasePaymentScheduleLines_Schedule_Period");

        var contractForeignKey = schedule.GetForeignKeys().Single(foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(LeaseContract));
        Assert.Equal(
            new[] { nameof(LeasePaymentSchedule.FinancialBookId), nameof(LeasePaymentSchedule.LeaseContractId) },
            contractForeignKey.Properties.Select(property => property.Name));
        Assert.Equal(
            new[] { nameof(LeaseContract.FinancialBookId), nameof(LeaseContract.Id) },
            contractForeignKey.PrincipalKey.Properties.Select(property => property.Name));
        Assert.Equal(DeleteBehavior.Restrict, contractForeignKey.DeleteBehavior);

        var scheduleNavigation = schedule.FindNavigation(nameof(LeasePaymentSchedule.Payments));
        Assert.NotNull(scheduleNavigation);
        Assert.Equal(typeof(LeasePaymentScheduleLine), scheduleNavigation!.TargetEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, scheduleNavigation.ForeignKey.DeleteBehavior);
    }
}
