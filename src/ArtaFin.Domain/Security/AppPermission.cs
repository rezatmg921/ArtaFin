using ArtaFin.Domain.Common;

namespace ArtaFin.Domain.Security;

/// <summary>A stable, code-defined system permission that can be granted to roles.</summary>
public sealed class AppPermission : AggregateRoot
{
    public const int MaxCodeLength = 150;
    public const int MaxNameLength = 150;

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private AppPermission(
        Guid id,
        string code,
        string name,
        string? description,
        DateTimeOffset createdAt) : base(id)
    {
        Code = code;
        Name = name;
        Description = description;
        CreatedAt = createdAt;
    }

    public static AppPermission Create(
        Guid id,
        string code,
        string name,
        string? description,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Permission id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Permission code is required.", nameof(code));
        if (code.Length > MaxCodeLength ||
            code.Split('.').Length != 2 ||
            code.Split('.').Any(part => string.IsNullOrWhiteSpace(part) ||
                part.Any(character => !char.IsAsciiLetterOrDigit(character))))
            throw new ArgumentException(
                "Permission code must use the 'Area.Action' format and contain only ASCII letters and digits.",
                nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Permission name is required.", nameof(name));
        if (name.Trim().Length > MaxNameLength)
            throw new ArgumentException($"Permission name cannot exceed {MaxNameLength} characters.", nameof(name));

        return new AppPermission(id, code.Trim(), name.Trim(), description?.Trim(), createdAt);
    }
}
