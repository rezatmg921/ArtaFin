using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests.Finance;

public sealed class LeasePaymentScheduleCalculatorTests
{
    [Fact]
    public void Calculate_UsesEachDatedPaymentAndCarriesClosingLiabilityForward()
    {
        var payments = new[]
        {
            new LeaseScheduledPayment(new DateOnly(2027, 1, 31), 10_000_000m, 0.01m),
            new LeaseScheduledPayment(new DateOnly(2027, 2, 28), 10_000_000m, 0.0125m)
        };

        var result = LeasePaymentScheduleCalculator.Calculate(
            new Money(80_000_000m, "IRR"),
            new DateOnly(2027, 1, 31),
            new DateOnly(2027, 12, 31),
            payments);

        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0].PeriodNumber);
        Assert.Equal(new DateOnly(2027, 1, 31), result[0].PaymentDate);
        Assert.Equal(800_000m, result[0].Calculation.InterestExpense.Amount);
        Assert.Equal(9_200_000m, result[0].Calculation.PrincipalReduction.Amount);
        Assert.Equal(70_800_000m, result[0].Calculation.ClosingLiability.Amount);
        Assert.Equal(885_000m, result[1].Calculation.InterestExpense.Amount);
        Assert.Equal(9_115_000m, result[1].Calculation.PrincipalReduction.Amount);
        Assert.Equal(61_685_000m, result[1].Calculation.ClosingLiability.Amount);
    }

    [Fact]
    public void Calculate_AllowsExplicitZeroPaymentAndNegativeAmortisation()
    {
        var result = LeasePaymentScheduleCalculator.Calculate(
            new Money(80_000_000m, "IRR"),
            new DateOnly(2027, 1, 1),
            new DateOnly(2027, 12, 31),
            [
                new LeaseScheduledPayment(new DateOnly(2027, 1, 1), 0m, 0.01m),
                new LeaseScheduledPayment(new DateOnly(2027, 2, 1), 500_000m, 0.01m)
            ]);

        Assert.Equal(800_000m, result[0].Calculation.InterestExpense.Amount);
        Assert.Equal(80_800_000m, result[0].Calculation.ClosingLiability.Amount);
        Assert.Equal(808_000m, result[1].Calculation.InterestExpense.Amount);
        Assert.Equal(-308_000m, result[1].Calculation.PrincipalReduction.Amount);
        Assert.Equal(81_108_000m, result[1].Calculation.ClosingLiability.Amount);
    }

    [Fact]
    public void Calculate_RejectsEmptySchedule()
    {
        Assert.Throws<ArgumentException>(() =>
            LeasePaymentScheduleCalculator.Calculate(
                new Money(1_000m, "IRR"),
                new DateOnly(2027, 1, 1),
                new DateOnly(2027, 12, 31),
                []));
    }

    [Fact]
    public void Calculate_RejectsUnorderedOrDuplicateDates()
    {
        var payments = new[]
        {
            new LeaseScheduledPayment(new DateOnly(2027, 2, 1), 100m, 0.01m),
            new LeaseScheduledPayment(new DateOnly(2027, 2, 1), 100m, 0.01m)
        };

        Assert.Throws<ArgumentException>(() =>
            LeasePaymentScheduleCalculator.Calculate(
                new Money(1_000m, "IRR"),
                new DateOnly(2027, 1, 1),
                new DateOnly(2027, 12, 31),
                payments));
    }

    [Fact]
    public void Calculate_RejectsPaymentOutsideContractDateRange()
    {
        Assert.Throws<ArgumentException>(() =>
            LeasePaymentScheduleCalculator.Calculate(
                new Money(1_000m, "IRR"),
                new DateOnly(2027, 1, 1),
                new DateOnly(2027, 12, 31),
                [new LeaseScheduledPayment(new DateOnly(2028, 1, 1), 100m, 0.01m)]));
    }

    [Fact]
    public void Calculate_RejectsRateOutsideSupportedPrecision()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LeasePaymentScheduleCalculator.Calculate(
                new Money(1_000m, "IRR"),
                new DateOnly(2027, 1, 1),
                new DateOnly(2027, 12, 31),
                [new LeaseScheduledPayment(new DateOnly(2027, 1, 1), 100m, 1_000_000_000m)]));
    }
}
