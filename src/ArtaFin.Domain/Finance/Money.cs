namespace ArtaFin.Domain.Finance;

/// <summary>
/// Currency-aware monetary value. Current persistence contract is decimal(19,4):
/// amounts are rejected (never silently rounded) when they exceed 15 integral digits
/// or 4 fractional digits. This is a provisional technical bound, not a final
/// currency-specific rounding policy.
/// </summary>
public readonly record struct Money
{
    public const int MaxScale = 4;
    public const decimal MaxAbsoluteAmount = 999999999999999.9999m;

    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3 ||
            currency.Any(c => !char.IsAsciiLetter(c)))
            throw new ArgumentException("Currency must be a three-letter ISO-style code.", nameof(currency));

        if (decimal.Abs(amount) > MaxAbsoluteAmount)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount exceeds the current decimal(19,4) persistence limit.");

        var scale = (decimal.GetBits(amount)[3] >> 16) & 0x7F;
        if (scale > MaxScale)
            throw new ArgumentOutOfRangeException(nameof(amount), $"Amount cannot have more than {MaxScale} fractional digits.");

        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (!StringComparer.Ordinal.Equals(left.Currency, right.Currency))
            throw new InvalidOperationException("Cannot combine amounts with different currencies.");
    }
}
