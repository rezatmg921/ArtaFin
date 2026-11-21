namespace ArtaFin.Application.Features.Idempotency;

/// <summary>
/// Raised when a client reuses an idempotency key for a different financial request.
/// </summary>
public sealed class IdempotencyKeyConflictException : Exception
{
    public IdempotencyKeyConflictException()
        : base("The idempotency key has already been used for a different request.")
    {
    }
}
