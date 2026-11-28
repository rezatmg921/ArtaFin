using ArtaFin.Infrastructure.Security;
using Xunit;

namespace ArtaFin.UnitTests.Infrastructure;

public sealed class RefreshTokenServiceTests
{
    private readonly RefreshTokenService _service = new();

    [Fact]
    public void GenerateToken_ShouldReturnHighEntropyUrlSafeTokens()
    {
        var first = _service.GenerateToken();
        var second = _service.GenerateToken();

        Assert.NotEqual(first, second);
        Assert.Equal(43, first.Length);
        Assert.Matches("^[A-Za-z0-9_-]+$", first);
    }

    [Fact]
    public void ComputeHash_ShouldBeDeterministicAndDifferentFromRawToken()
    {
        const string token = "sample-refresh-token";

        var firstHash = _service.ComputeHash(token);
        var secondHash = _service.ComputeHash(token);

        Assert.Equal(firstHash, secondHash);
        Assert.NotEqual(token, firstHash);
        Assert.Equal(64, firstHash.Length);
    }
}
