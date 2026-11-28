using System.Net.Mail;
using ArtaFin.Domain.Common;

namespace ArtaFin.Domain.Contacts;

/// <summary>
/// A person or organization participating in transactions inside one FinancialBook.
/// Contact identity is book-scoped; the same real-world party may exist in multiple books.
/// </summary>
public sealed class Contact : AggregateRoot
{
    public const int MaxDisplayNameLength = 200;
    public const int MaxEmailLength = 254;
    public const int MaxPhoneLength = 32;
    public const int MaxNotesLength = 1000;

    public Guid FinancialBookId { get; }
    public ContactType Type { get; private set; }
    public string DisplayName { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }
    public bool IsArchived => ArchivedAt.HasValue;

    private Contact(
        Guid id, Guid financialBookId, ContactType type, string displayName,
        string? email, string? phone, string? notes, DateTimeOffset createdAt)
        : base(id)
    {
        FinancialBookId = financialBookId;
        Type = type;
        DisplayName = displayName;
        Email = email;
        Phone = phone;
        Notes = notes;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static Contact Create(
        Guid id, Guid financialBookId, ContactType type, string displayName,
        string? email, string? phone, string? notes, DateTimeOffset createdAt)
    {
        if (financialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(financialBookId));
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type));

        var normalizedName = ValidateDisplayName(displayName);
        var normalizedEmail = NormalizeEmail(email);
        var normalizedPhone = NormalizePhone(phone);
        var normalizedNotes = NormalizeNotes(notes);

        return new Contact(id, financialBookId, type, normalizedName,
            normalizedEmail, normalizedPhone, normalizedNotes, createdAt);
    }

    public void UpdateDetails(
        ContactType type, string displayName, string? email,
        string? phone, string? notes, DateTimeOffset updatedAt)
    {
        EnsureActive();
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type));

        if (updatedAt < UpdatedAt)
            throw new DomainException("Contact update time cannot be earlier than its previous update.");

        // Validate every value before mutating the aggregate so a rejected update
        // cannot leave a partially changed in-memory entity.
        var normalizedName = ValidateDisplayName(displayName);
        var normalizedEmail = NormalizeEmail(email);
        var normalizedPhone = NormalizePhone(phone);
        var normalizedNotes = NormalizeNotes(notes);

        Type = type;
        DisplayName = normalizedName;
        Email = normalizedEmail;
        Phone = normalizedPhone;
        Notes = normalizedNotes;
        UpdatedAt = updatedAt;
    }

    public void Archive(DateTimeOffset archivedAt)
    {
        EnsureActive();
        if (archivedAt < UpdatedAt)
            throw new DomainException("Contact cannot be archived before its last update.");
        ArchivedAt = archivedAt;
        UpdatedAt = archivedAt;
    }

    private void EnsureActive()
    {
        if (IsArchived)
            throw new DomainException("An archived contact cannot be changed.");
    }

    private static string ValidateDisplayName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Contact display name is required.", nameof(value));
        var normalized = value.Trim();
        if (normalized.Length > MaxDisplayNameLength)
            throw new ArgumentException($"Contact display name cannot exceed {MaxDisplayNameLength} characters.", nameof(value));
        if (normalized.Any(char.IsControl))
            throw new ArgumentException("Contact display name cannot contain control characters.", nameof(value));
        return normalized;
    }

    private static string? NormalizeEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > MaxEmailLength || !MailAddress.TryCreate(normalized, out var address) ||
            !string.Equals(address.Address, normalized, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A valid email address is required.", nameof(value));
        return normalized;
    }

    private static string? NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var normalized = value.Trim();
        if (normalized.Length > MaxPhoneLength ||
            normalized.Any(character => !char.IsAsciiDigit(character) &&
                character is not '+' and not '-' and not '(' and not ')' and not ' '))
            throw new ArgumentException("Phone number contains unsupported characters or exceeds the maximum length.", nameof(value));
        if (!normalized.Any(char.IsAsciiDigit))
            throw new ArgumentException("Phone number must contain at least one digit.", nameof(value));
        return normalized;
    }

    private static string? NormalizeNotes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var normalized = value.Trim();
        if (normalized.Length > MaxNotesLength)
            throw new ArgumentException($"Contact notes cannot exceed {MaxNotesLength} characters.", nameof(value));
        return normalized;
    }
}
