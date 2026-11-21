using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests.Domain.Finance;

public sealed class CurrencyTests
{
    [Theory]
    [InlineData("eur", "EUR", 2)]
    [InlineData(" JPY ", "JPY", 0)]
    [InlineData("kwd", "KWD", 3)]
    [InlineData("clf", "CLF", 4)]
    [InlineData("irr", "IRR", 2)]
    public void FromCode_NormalizesCodeAndProvidesMinorUnit(
        string input, string expectedCode, int expectedMinorUnit)
    {
        var currency = Currency.FromCode(input);

        Assert.Equal(expectedCode, currency.Code);
        Assert.Equal(expectedMinorUnit, currency.MinorUnit);
    }

    [Theory]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("U$D")]
    public void FromCode_RejectsMalformedCodes(string code)
    {
        Assert.Throws<ArgumentException>(() => Currency.FromCode(code));
    }

    [Fact]
    public void FromCode_RejectsWellFormedButUnsupportedCodes()
    {
        Assert.Throws<NotSupportedException>(() => Currency.FromCode("ZZZ"));
    }

    [Fact]
    public void Money_RejectsDefaultCurrencyValue()
    {
        Assert.Throws<ArgumentException>(() => new Money(1m, default(Currency)));
    }
}
