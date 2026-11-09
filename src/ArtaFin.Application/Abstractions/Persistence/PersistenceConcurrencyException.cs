namespace ArtaFin.Application.Abstractions.Persistence;

/// <summary>
/// Indicates that a persistence operation lost an optimistic concurrency race.
/// This keeps EF Core-specific exceptions out of the Application layer.
/// </summary>
public sealed class PersistenceConcurrencyException : Exception
{
    public PersistenceConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
