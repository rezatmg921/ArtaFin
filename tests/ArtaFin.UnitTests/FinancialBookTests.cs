using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests;

public sealed class FinancialBookTests
{
    [Fact]
    public void Create_AssignsOwnerAndNormalizesFields()
    {
        var owner = Guid.NewGuid();
        var book = FinancialBook.Create(Guid.NewGuid(), owner, "  Home  ", "eur", DateTimeOffset.UtcNow);

        Assert.Equal(owner, book.OwnerId);
        Assert.Equal("Home", book.Name);
        Assert.Equal("EUR", book.BaseCurrency.Code);
        Assert.Equal(2, book.BaseCurrency.MinorUnit);
        Assert.True(book.IsOwnedBy(owner));
        Assert.False(book.IsOwnedBy(Guid.NewGuid()));
    }

    [Fact]
    public void Create_RejectsMissingOwner()
    {
        Assert.Throws<ArgumentException>(() => FinancialBook.Create(Guid.NewGuid(),
            Guid.Empty, "Home", "EUR", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Create_RejectsMissingName()
    {
        Assert.Throws<ArgumentException>(() => FinancialBook.Create(Guid.NewGuid(),
            Guid.NewGuid(), " ", "EUR", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Create_RejectsUnsupportedBaseCurrency()
    {
        Assert.Throws<NotSupportedException>(() => FinancialBook.Create(Guid.NewGuid(),
            Guid.NewGuid(), "Home", "ZZZ", DateTimeOffset.UtcNow));
    }
}
