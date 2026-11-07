namespace ArtaFin.Domain.Finance;

/// <summary>
/// An exact monetary amount in a catalogued ISO 4217 currency.
/// Amounts are never rounded implicitly. Values must fit the currency's minor-unit
/// precision and the SQL Server decimal(19,4) persistence boundary.
/// </summary>
public readonly record struct Money
{
    // Persistence limit: decimal(19,4) permits 15 integral and 4 fractional digits.
    public const int MaxScale = 4;
    public const decimal MaxAbsoluteAmount = 999999999999999.9999m;

    public decimal Amount { get; }
    public string Currency { get; }
    public Currency CurrencyInfo { get; }

    public Money(decimal amount, string currency)
        : this(amount, Currency.FromCode(currency))
    {
    }

    public Money(decimal amount, Currency currency)
    {
        currency.EnsureValid();

        if (amount < -MaxAbsoluteAmount || amount > MaxAbsoluteAmount)
            throw new ArgumentOutOfRangeException(nameof(amount),
                "Amount exceeds the decimal(19,4) persistence limit.");

        // Numeric comparison deliberately accepts insignificant trailing zeroes
        // (for example 1.2300 EUR) while rejecting non-zero excess precision.
        if (decimal.Round(amount, currency.MinorUnit, MidpointRounding.ToZero) != amount)
            throw new ArgumentOutOfRangeException(nameof(amount),
                $"Currency {currency.Code} supports at most {currency.MinorUnit} fractional digits.");

        Amount = amount;
        Currency = currency.Code;
        CurrencyInfo = currency;
    }

    /// <summary>
    /// Explicitly rounds a raw amount to the currency's minor-unit precision.
    /// Callers must choose a midpoint rule; construction and arithmetic never round.
    /// </summary>
    public static Money CreateRounded(decimal amount, Currency currency, MidpointRounding rounding)
    {
        currency.EnsureValid();
        var roundedAmount = decimal.Round(amount, currency.MinorUnit, rounding);
        return new Money(roundedAmount, currency);
    }

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.CurrencyInfo);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount - right.Amount, left.CurrencyInfo);
    }

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (!StringComparer.Ordinal.Equals(left.Currency, right.Currency))
            throw new InvalidOperationException("Cannot combine amounts with different currencies.");
    }
}
