using ArtaFin.Domain.Common;

namespace ArtaFin.Domain.Security;

/// <summary>A named ArtaFin authorization role.</summary>
public sealed class AppRole : AggregateRoot
{
    public const int MaxNameLength = 100;

    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private AppRole(
        Guid id,
        string name,
        string normalizedName,
        string? description,
        DateTimeOffset createdAt) : base(id)
    {
        Name = name;
        NormalizedName = normalizedName;
        Description = description;
        CreatedAt = createdAt;
    }

    public static AppRole Create(
        Guid id,
        string name,
        string? description,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Role id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Role name is required.", nameof(name));

        var normalizedName = name.Trim().ToUpperInvariant();
        if (normalizedName.Length > MaxNameLength)
            throw new ArgumentException($"Role name cannot exceed {MaxNameLength} characters.", nameof(name));

        return new AppRole(id, name.Trim(), normalizedName, description?.Trim(), createdAt);
    }
}
