using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Infrastructure.Security;

namespace ArtaFin.UnitTests.Infrastructure;

public sealed class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_ShouldProduceDifferentHashesForSamePassword()
    {
        const string password = "Correct-Horse-Battery-42!";

        var first = _hasher.Hash(password);
        var second = _hasher.Hash(password);

        Assert.NotEqual(first, second);
        Assert.StartsWith("$artafin$pbkdf2-sha256$v=1$", first);
    }

    [Fact]
    public void Verify_ShouldSucceedForCorrectPassword()
    {
        const string password = "Correct-Horse-Battery-42!";
        var hash = _hasher.Hash(password);

        var result = _hasher.Verify(password, hash);

        Assert.Equal(PasswordVerificationResult.Success, result);
    }

    [Fact]
    public void Verify_ShouldFailForIncorrectPassword()
    {
        var hash = _hasher.Hash("Correct-Horse-Battery-42!");

        var result = _hasher.Verify("Incorrect-Password-42!", hash);

        Assert.Equal(PasswordVerificationResult.Failed, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-valid-hash")]
    [InlineData("$artafin$pbkdf2-sha256$v=1$i=999999999$l=32$c2FsdA==$aGFzaA==")]
    public void Verify_ShouldFailForMalformedOrUnsupportedHash(string encodedHash)
    {
        var result = _hasher.Verify("Any-Password-42!", encodedHash);

        Assert.Equal(PasswordVerificationResult.Failed, result);
    }
}
