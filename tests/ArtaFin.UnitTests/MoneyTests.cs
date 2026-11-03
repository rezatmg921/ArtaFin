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
    public void Construction_WithInvalidCurrency_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Money(1m, "IR"));
    }
}
