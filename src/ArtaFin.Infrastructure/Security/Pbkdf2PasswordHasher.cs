using System.Globalization;
using System.Security.Cryptography;
using ArtaFin.Application.Abstractions.Security;

namespace ArtaFin.Infrastructure.Security;

/// <summary>
/// Versioned PBKDF2-HMAC-SHA256 password hashing implementation.
/// Encoded format: $artafin$pbkdf2-sha256$v=1$i=600000$l=32$&lt;salt&gt;$&lt;hash&gt;
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string FormatMarker = "artafin";
    private const string AlgorithmMarker = "pbkdf2-sha256";
    private const int FormatVersion = 1;
    private const int CurrentIterations = 600_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int MaximumAcceptedIterations = 2_000_000;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            CurrentIterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return string.Join('$',
            string.Empty,
            FormatMarker,
            AlgorithmMarker,
            $"v={FormatVersion.ToString(CultureInfo.InvariantCulture)}",
            $"i={CurrentIterations.ToString(CultureInfo.InvariantCulture)}",
            $"l={HashSize.ToString(CultureInfo.InvariantCulture)}",
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public PasswordVerificationResult Verify(string password, string encodedHash)
    {
        ArgumentNullException.ThrowIfNull(password);
        if (string.IsNullOrEmpty(encodedHash))
            return PasswordVerificationResult.Failed;

        if (!TryParse(encodedHash, out var iterations, out var expectedHash, out var salt))
            return PasswordVerificationResult.Failed;

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length);

        if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
            return PasswordVerificationResult.Failed;

        return iterations < CurrentIterations
            ? PasswordVerificationResult.SuccessRehashNeeded
            : PasswordVerificationResult.Success;
    }

    private static bool TryParse(
        string encodedHash,
        out int iterations,
        out byte[] expectedHash,
        out byte[] salt)
    {
        iterations = 0;
        expectedHash = [];
        salt = [];

        var parts = encodedHash.Split('$');
        if (parts.Length != 8 ||
            parts[0].Length != 0 ||
            parts[1] != FormatMarker ||
            parts[2] != AlgorithmMarker ||
            parts[3] != $"v={FormatVersion}" ||
            !parts[4].StartsWith("i=", StringComparison.Ordinal) ||
            !parts[5].StartsWith("l=", StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(parts[4].AsSpan(2), NumberStyles.None,
                CultureInfo.InvariantCulture, out iterations) ||
            iterations < 100_000 ||
            iterations > MaximumAcceptedIterations)
        {
            return false;
        }

        if (!int.TryParse(parts[5].AsSpan(2), NumberStyles.None,
                CultureInfo.InvariantCulture, out var hashLength) ||
            hashLength != HashSize)
        {
            return false;
        }

        try
        {
            salt = Convert.FromBase64String(parts[6]);
            expectedHash = Convert.FromBase64String(parts[7]);
        }
        catch (FormatException)
        {
            return false;
        }

        return salt.Length == SaltSize && expectedHash.Length == hashLength;
    }
}
