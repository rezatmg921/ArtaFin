using ArtaFin.Domain.Common;

namespace ArtaFin.Domain.Security;

/// <summary>Represents a local ArtaFin account, independent of any identity framework.</summary>
public sealed class AppUser : AggregateRoot
{
    public const int MaxEmailLength = 320;

    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DisabledAt { get; private set; }

    private AppUser(
        Guid id,
        string email,
        string normalizedEmail,
        bool isActive,
        DateTimeOffset createdAt,
        DateTimeOffset? disabledAt) : base(id)
    {
        Email = email;
        NormalizedEmail = normalizedEmail;
        IsActive = isActive;
        CreatedAt = createdAt;
        DisabledAt = disabledAt;
    }

    public static AppUser Register(Guid id, string email, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("User id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));

        var normalizedEmail = email.Trim().ToUpperInvariant();
        if (normalizedEmail.Length > MaxEmailLength)
            throw new ArgumentException($"Email cannot exceed {MaxEmailLength} characters.", nameof(email));

        return new AppUser(id, email.Trim(), normalizedEmail, true, createdAt, null);
    }

    public void Disable(DateTimeOffset disabledAt)
    {
        if (!IsActive)
            return;

        IsActive = false;
        DisabledAt = disabledAt;
    }

    public void Reactivate()
    {
        IsActive = true;
        DisabledAt = null;
    }

    public bool HasEmail(string normalizedEmail) =>
        string.Equals(NormalizedEmail, normalizedEmail, StringComparison.Ordinal);
}
