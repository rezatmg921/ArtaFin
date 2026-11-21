using System.Security.Cryptography;
using System.Text;

namespace ArtaFin.Application.Features.Idempotency;

/// <summary>Creates a stable SHA-256 fingerprint for a normalized financial request.</summary>
public static class IdempotencyRequestHash
{
    public static string Compute(params string[] values)
    {
        ArgumentNullException.ThrowIfNull(values);

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            foreach (var value in values)
                writer.Write(value ?? string.Empty);
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    public static void ValidateKey(string? key)
    {
        if (key is not null &&
            (string.IsNullOrWhiteSpace(key) || key.Length > 128 || key.Any(char.IsControl)))
        {
            throw new ArgumentException(
                "Idempotency key must contain 1 to 128 non-control characters.",
                nameof(key));
        }
    }
}
