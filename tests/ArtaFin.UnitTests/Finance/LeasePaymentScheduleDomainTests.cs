using ArtaFin.Domain.Common;
using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests.Finance;

public sealed class LeasePaymentScheduleDomainTests
{
    private static LeaseContract CreateContract() =>
        LeaseContract.Create(
            Guid.NewGuid(), Guid.NewGuid(), "LEASE-001", "Office equipment lease",
            new DateOnly(2027, 1, 1), new DateOnly(2027, 2, 1), new DateOnly(2027, 12, 31),
            new Money(80_000_000m, "IRR"), 0.01m,
            LeasePaymentFrequency.Monthly, LeasePaymentTiming.InArrears,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void Create_StoresDatedPaymentsInSequentialPeriods()
    {
        var contract = CreateContract();
        var schedule = LeasePaymentSchedule.Create(
            Guid.NewGuid(), contract, 1, null, Guid.NewGuid(), DateTimeOffset.UtcNow,
            [
                new LeaseScheduledPayment(new DateOnly(2027, 2, 1), 10_000_000m, 0.01m),
                new LeaseScheduledPayment(new DateOnly(2027, 3, 1), 10_000_000m, 0.012m)
            ]);

        Assert.Equal(1, schedule.Version);
        Assert.Equal(LeasePaymentScheduleStatus.Draft, schedule.Status);
        Assert.Equal(2, schedule.Payments.Count);
        Assert.Equal(1, schedule.Payments[0].PeriodNumber);
        Assert.Equal(2, schedule.Payments[1].PeriodNumber);
        Assert.Equal(new DateOnly(2027, 3, 1), schedule.Payments[1].PaymentDate);
        Assert.Equal(0.012m, schedule.Payments[1].EffectivePeriodicRate);
    }

    [Fact]
    public void Create_RejectsScheduleThatWouldOverpayTheLiability()
    {
        var contract = CreateContract();
        Assert.Throws<ArgumentException>(() =>
            LeasePaymentSchedule.Create(
                Guid.NewGuid(), contract, 1, null, Guid.NewGuid(), DateTimeOffset.UtcNow,
                [new LeaseScheduledPayment(new DateOnly(2027, 2, 1), 81_000_001m, 0.01m)]));
    }

    [Fact]
    public void Finalize_TransitionsDraftAndRecordsActorAndTime()
    {
        var contract = CreateContract();
        var schedule = LeasePaymentSchedule.Create(
            Guid.NewGuid(), contract, 1, null, Guid.NewGuid(), DateTimeOffset.UtcNow,
            [new LeaseScheduledPayment(new DateOnly(2027, 2, 1), 10_000_000m, 0.01m)]);
        var actor = Guid.NewGuid();
        var finalizedAt = DateTimeOffset.UtcNow;

        schedule.Finalize(actor, finalizedAt);

        Assert.Equal(LeasePaymentScheduleStatus.Finalized, schedule.Status);
        Assert.Equal(actor, schedule.FinalizedBy);
        Assert.Equal(finalizedAt, schedule.FinalizedAt);
        Assert.Throws<DomainException>(() => schedule.Finalize(actor, finalizedAt));
    }

    [Fact]
    public void MarkSuperseded_OnlyAllowsFinalizedSchedule()
    {
        var contract = CreateContract();
        var schedule = LeasePaymentSchedule.Create(
            Guid.NewGuid(), contract, 1, null, Guid.NewGuid(), DateTimeOffset.UtcNow,
            [new LeaseScheduledPayment(new DateOnly(2027, 2, 1), 10_000_000m, 0.01m)]);

        Assert.Throws<DomainException>(() => schedule.MarkSuperseded());
        schedule.Finalize(Guid.NewGuid(), DateTimeOffset.UtcNow);
        schedule.MarkSuperseded();

        Assert.Equal(LeasePaymentScheduleStatus.Superseded, schedule.Status);
    }
}
