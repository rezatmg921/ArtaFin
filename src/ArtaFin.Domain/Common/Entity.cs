namespace ArtaFin.Domain.Common;

/// <summary>Base type for domain entities with stable identity.</summary>
public abstract class Entity
{
    protected Entity(Guid id) => Id = id != Guid.Empty
        ? id
        : throw new ArgumentException("Entity id cannot be empty.", nameof(id));

    public Guid Id { get; }
}
