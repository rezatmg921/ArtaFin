using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests.Domain.Finance;

public sealed class MoneyPersistencePrecisionTests
{
    [Fact]
    public void Money_AcceptsAmountsAtCurrencyMinorUnitPrecision()
    {
        Assert.Equal(12.34m, new Money(12.34m, "USD").Amount);
        Assert.Equal(12.345m, new Money(12.345m, "KWD").Amount);
        Assert.Equal(12.3456m, new Money(12.3456m, "CLF").Amount);
        Assert.Equal(12m, new Money(12m, "JPY").Amount);
    }

    [Fact]
    public void Money_RejectsNonZeroDigitsBeyondCurrencyMinorUnit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(1.001m, "EUR"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(1.01m, "JPY"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(1.2345m, "KWD"));
    }

    [Fact]
    public void Money_AcceptsInsignificantTrailingZeroes()
    {
        var money = new Money(1.2300m, "EUR");

        Assert.Equal(1.2300m, money.Amount);
    }

    [Fact]
    public void Money_RejectsAmountsOutsideDecimalNineteenFourRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Money(1000000000000000m, "CLF"));
    }

    [Fact]
    public void Money_AcceptsMaximumDecimalNineteenFourAmountForFourDigitCurrency()
    {
        var money = new Money(999999999999999.9999m, "CLF");

        Assert.Equal(999999999999999.9999m, money.Amount);
    }

    [Fact]
    public void Money_CreateRounded_RequiresAnExplicitMidpointRule()
    {
        var currency = Currency.FromCode("EUR");

        var toEven = Money.CreateRounded(1.005m, currency, MidpointRounding.ToEven);
        var awayFromZero = Money.CreateRounded(1.005m, currency, MidpointRounding.AwayFromZero);

        Assert.Equal(1.00m, toEven.Amount);
        Assert.Equal(1.01m, awayFromZero.Amount);
    }
}
