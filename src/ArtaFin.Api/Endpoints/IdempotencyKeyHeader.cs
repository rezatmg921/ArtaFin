namespace ArtaFin.Api.Endpoints;

internal static class IdempotencyKeyHeader
{
    private const string HeaderName = "Idempotency-Key";

    public static string? Read(
        HttpRequest request,
        IDictionary<string, string[]> validationErrors)
    {
        if (!request.Headers.TryGetValue(HeaderName, out var values))
            return null;

        if (values.Count != 1)
        {
            validationErrors[HeaderName] = ["Exactly one Idempotency-Key header value is allowed."];
            return null;
        }

        var key = values[0]?.Trim();
        if (string.IsNullOrWhiteSpace(key) || key.Length > 128 || key.Any(char.IsControl))
        {
            validationErrors[HeaderName] = ["Idempotency-Key must contain 1 to 128 non-control characters."];
            return null;
        }

        return key;
    }
}
