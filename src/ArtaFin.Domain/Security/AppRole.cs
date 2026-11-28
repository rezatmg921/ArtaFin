using ArtaFin.Domain.Common;

namespace ArtaFin.Domain.Security;

/// <summary>A named ArtaFin authorization role.</summary>
public sealed class AppRole : AggregateRoot
{
    public const int MaxNameLength = 100;
    public const int MaxDescriptionLength = 500;

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

        var (normalizedName, trimmedName, trimmedDescription) =
            ValidateAndNormalize(name, description);

        return new AppRole(id, trimmedName, normalizedName, trimmedDescription, createdAt);
    }

    public void UpdateDetails(string name, string? description)
    {
        var (normalizedName, trimmedName, trimmedDescription) =
            ValidateAndNormalize(name, description);

        Name = trimmedName;
        NormalizedName = normalizedName;
        Description = trimmedDescription;
    }

    private static (string NormalizedName, string Name, string? Description)
        ValidateAndNormalize(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Role name is required.", nameof(name));

        var trimmedName = name.Trim();
        if (trimmedName.Length > MaxNameLength)
            throw new ArgumentException($"Role name cannot exceed {MaxNameLength} characters.", nameof(name));

        var trimmedDescription = description?.Trim();
        if (trimmedDescription?.Length > MaxDescriptionLength)
            throw new ArgumentException(
                $"Role description cannot exceed {MaxDescriptionLength} characters.",
                nameof(description));

        return (trimmedName.ToUpperInvariant(), trimmedName, trimmedDescription);
    }
}
