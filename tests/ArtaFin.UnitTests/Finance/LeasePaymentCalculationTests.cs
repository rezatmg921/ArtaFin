using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests.Finance;

public sealed class LeasePaymentCalculationTests
{
    [Fact]
    public void Calculate_SplitsPaymentIntoInterestAndPrincipal()
    {
        var opening = new Money(80_000_000m, "IRR");
        var payment = new Money(10_000_000m, "IRR");

        var result = LeasePaymentCalculation.Calculate(opening, 0.01m, payment);

        Assert.Equal(800_000m, result.InterestExpense.Amount);
        Assert.Equal(9_200_000m, result.PrincipalReduction.Amount);
        Assert.Equal(70_800_000m, result.ClosingLiability.Amount);
        Assert.Equal("IRR", result.ClosingLiability.Currency);
    }

    [Fact]
    public void Calculate_PreservesNegativeAmortisationWhenPaymentIsBelowInterest()
    {
        var opening = new Money(80_000_000m, "IRR");
        var payment = new Money(500_000m, "IRR");

        var result = LeasePaymentCalculation.Calculate(opening, 0.01m, payment);

        Assert.Equal(800_000m, result.InterestExpense.Amount);
        Assert.Equal(-300_000m, result.PrincipalReduction.Amount);
        Assert.Equal(80_300_000m, result.ClosingLiability.Amount);
    }

    [Fact]
    public void Calculate_RoundsInterestToCurrencyMinorUnitAwayFromZero()
    {
        var opening = new Money(100m, "IRR");
        var payment = new Money(1m, "IRR");

        var result = LeasePaymentCalculation.Calculate(opening, 0.005m, payment);

        Assert.Equal(0.50m, result.InterestExpense.Amount);
    }

    [Fact]
    public void Calculate_RejectsPaymentThatExceedsLiabilityAndInterest()
    {
        var opening = new Money(80_000_000m, "IRR");
        var payment = new Money(81_000_001m, "IRR");

        Assert.Throws<ArgumentException>(() =>
            LeasePaymentCalculation.Calculate(opening, 0.01m, payment));
    }

    [Fact]
    public void Calculate_RejectsDifferentCurrencies()
    {
        var opening = new Money(80_000_000m, "IRR");
        var payment = new Money(10m, "USD");

        Assert.Throws<ArgumentException>(() =>
            LeasePaymentCalculation.Calculate(opening, 0.01m, payment));
    }
}
