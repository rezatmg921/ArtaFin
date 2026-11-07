using ArtaFin.Domain.Common;

namespace ArtaFin.Domain.Finance;

/// <summary>A personal or shared ledger boundary with an explicit owner.</summary>
public sealed class FinancialBook : AggregateRoot
{
    public const int MaxNameLength = 200;

    public Guid OwnerId { get; }
    public string Name { get; }
    public Currency BaseCurrency { get; }
    public DateTimeOffset CreatedAt { get; }

    private FinancialBook(Guid id, Guid ownerId, string name, Currency baseCurrency,
        DateTimeOffset createdAt) : base(id)
    {
        OwnerId = ownerId;
        Name = name;
        BaseCurrency = baseCurrency;
        CreatedAt = createdAt;
    }

    public static FinancialBook Create(Guid id, Guid ownerId, string name,
        string baseCurrency, DateTimeOffset createdAt)
    {
        if (ownerId == Guid.Empty)
            throw new ArgumentException("Owner id cannot be empty.", nameof(ownerId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Financial book name is required.", nameof(name));

        var normalizedName = name.Trim();
        if (normalizedName.Length > MaxNameLength)
            throw new ArgumentException($"Financial book name cannot exceed {MaxNameLength} characters.", nameof(name));

        var currency = Currency.FromCode(baseCurrency);
        return new FinancialBook(id, ownerId, normalizedName, currency, createdAt);
    }

    public bool IsOwnedBy(Guid userId) => userId != Guid.Empty && OwnerId == userId;
}
