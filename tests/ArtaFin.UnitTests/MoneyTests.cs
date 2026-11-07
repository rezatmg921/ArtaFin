using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests;

public sealed class MoneyTests
{
    [Fact]
    public void Addition_WithSameCurrency_ReturnsSum()
    {
        var result = new Money(125m, "IRR") + new Money(75m, "irr");

        Assert.Equal(200m, result.Amount);
        Assert.Equal("IRR", result.Currency);
    }

    [Fact]
    public void Addition_WithDifferentCurrencies_Throws()
    {
        var rial = new Money(100m, "IRR");
        var usd = new Money(1m, "USD");

        Assert.Throws<InvalidOperationException>(() => _ = rial + usd);
    }

    [Fact]
    public void Subtraction_WithSameCurrency_ReturnsDifference()
    {
        var result = new Money(10.50m, "EUR") - new Money(2.25m, "EUR");

        Assert.Equal(8.25m, result.Amount);
        Assert.Equal("EUR", result.Currency);
    }

    [Fact]
    public void Arithmetic_DoesNotSilentlyRound()
    {
        var amount = new Money(1.23m, "EUR");
        var thirdFraction = new Money(0.001m, "CLF");

        Assert.Throws<InvalidOperationException>(() => _ = amount + thirdFraction);
    }

    [Fact]
    public void Construction_WithInvalidCurrency_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Money(1m, "US"));
    }

    [Fact]
    public void Construction_WithUnsupportedCurrency_Throws()
    {
        Assert.Throws<NotSupportedException>(() => new Money(1m, "ZZZ"));
    }
}
