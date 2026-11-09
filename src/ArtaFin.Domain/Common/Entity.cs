namespace ArtaFin.Domain.Common;

/// <summary>Base type for domain entities with stable identity.</summary>
public abstract class Entity : IEquatable<Entity>
{
    protected Entity(Guid id) => Id = id != Guid.Empty
        ? id
        : throw new ArgumentException("Entity id cannot be empty.", nameof(id));

    public Guid Id { get; }

    public bool Equals(Entity? other) =>
        other is not null &&
        GetType() == other.GetType() &&
        Id == other.Id;

    public override bool Equals(object? obj) =>
        obj is Entity entity && Equals(entity);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
