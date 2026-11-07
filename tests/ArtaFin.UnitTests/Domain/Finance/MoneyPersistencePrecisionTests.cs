using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests.Domain.Finance;

public sealed class MoneyPersistencePrecisionTests
{
    [Fact]
    public void Money_AcceptsFourFractionalDigits()
    {
        var money = new Money(12.3456m, "USD");
        Assert.Equal(12.3456m, money.Amount);
    }

    [Fact]
    public void Money_RejectsMoreThanFourFractionalDigitsInsteadOfRounding()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(1.00001m, "USD"));
    }

    [Fact]
    public void Money_RejectsAmountsOutsideDecimalNineteenFourRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(1000000000000000m, "USD"));
    }
}
