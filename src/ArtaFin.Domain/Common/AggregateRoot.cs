namespace ArtaFin.Domain.Common;

/// <summary>
/// Marks the consistency boundary entry point of an aggregate.
/// External domain objects should reference an aggregate root by identity,
/// rather than holding references to its internal entities.
/// </summary>
public abstract class AggregateRoot(Guid id) : Entity(id)
{
}
