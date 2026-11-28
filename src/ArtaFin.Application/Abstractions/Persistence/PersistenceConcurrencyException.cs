namespace ArtaFin.Application.Abstractions.Persistence;

/// <summary>
/// Indicates that a persistence operation lost an optimistic concurrency race.
/// This keeps EF Core-specific exceptions out of the Application layer.
/// </summary>
public sealed class PersistenceConcurrencyException : Exception
{
    public PersistenceConcurrencyException(
        string message,
        Exception innerException,
        string? diagnosticDetails = null)
        : base(message, innerException)
    {
        DiagnosticDetails = diagnosticDetails;
    }

    /// <summary>
    /// Safe persistence metadata to help identify the entity involved in a
    /// concurrency failure without exposing database values or rowversion bytes.
    /// </summary>
    public string? DiagnosticDetails { get; }
}
