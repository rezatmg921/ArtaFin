using ArtaFin.Application.Features.Reports.GetBalanceSheet;
using Xunit;

namespace ArtaFin.UnitTests;

public sealed class ObligationMaturityCalculatorTests
{
    [Fact]
    public void Calculate_SplitsOutstandingInstallmentsAtTwelveMonthBoundary()
    {
        var asOf = new DateOnly(2026, 11, 27);
        var currentId = Guid.NewGuid();
        var nonCurrentId = Guid.NewGuid();

        var result = ObligationMaturityCalculator.Calculate(
            asOf,
            [
                new ObligationInstallmentTerm(currentId, new DateOnly(2027, 11, 27), 30m),
                new ObligationInstallmentTerm(nonCurrentId, new DateOnly(2027, 11, 28), 70m)
            ],
            []);

        Assert.Equal(30m, result.CurrentAmount);
        Assert.Equal(70m, result.NonCurrentAmount);
        Assert.Equal(100m, result.TotalOutstanding);
    }

    [Fact]
    public void Calculate_SubtractsPaymentsFromTheirInstallments()
    {
        var currentId = Guid.NewGuid();
        var nonCurrentId = Guid.NewGuid();

        var result = ObligationMaturityCalculator.Calculate(
            new DateOnly(2026, 11, 27),
            [
                new ObligationInstallmentTerm(currentId, new DateOnly(2027, 2, 1), 50m),
                new ObligationInstallmentTerm(nonCurrentId, new DateOnly(2028, 2, 1), 100m)
            ],
            [
                new ObligationInstallmentPayment(currentId, 20m),
                new ObligationInstallmentPayment(nonCurrentId, 25m)
            ]);

        Assert.Equal(30m, result.CurrentAmount);
        Assert.Equal(75m, result.NonCurrentAmount);
        Assert.Equal(105m, result.TotalOutstanding);
    }

    [Fact]
    public void Calculate_TreatsOverdueInstallmentsAsCurrent()
    {
        var installmentId = Guid.NewGuid();

        var result = ObligationMaturityCalculator.Calculate(
            new DateOnly(2026, 11, 27),
            [new ObligationInstallmentTerm(installmentId, new DateOnly(2026, 10, 1), 40m)],
            []);

        Assert.Equal(40m, result.CurrentAmount);
        Assert.Equal(0m, result.NonCurrentAmount);
    }

    [Fact]
    public void Calculate_RejectsPaymentsGreaterThanInstallmentAmount()
    {
        var installmentId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => ObligationMaturityCalculator.Calculate(
            new DateOnly(2026, 11, 27),
            [new ObligationInstallmentTerm(installmentId, new DateOnly(2027, 1, 1), 40m)],
            [new ObligationInstallmentPayment(installmentId, 41m)]));
    }
}
