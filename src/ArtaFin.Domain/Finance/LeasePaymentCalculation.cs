namespace ArtaFin.Domain.Finance;

/// <summary>
/// Result of one lease-liability effective-interest period.
/// PrincipalReduction is signed: a negative value represents negative amortisation
/// when the contractual payment is lower than the interest accrued for the period.
/// </summary>
public sealed record LeasePaymentCalculation(
    Money OpeningLiability,
    Money Payment,
    Money InterestExpense,
    Money PrincipalReduction,
    Money ClosingLiability)
{
    /// <summary>
    /// Calculates one period using an effective periodic rate.
    /// The caller is responsible for deriving that rate from the executed lease terms
    /// and for selecting the period boundaries/day-count convention.
    /// Interest is rounded once to the currency's minor unit using AwayFromZero.
    /// </summary>
    public static LeasePaymentCalculation Calculate(
        Money openingLiability,
        decimal effectivePeriodicRate,
        Money payment)
    {
        if (!StringComparer.Ordinal.Equals(openingLiability.Currency, payment.Currency))
            throw new ArgumentException("Opening liability and payment must use the same currency.", nameof(payment));

        if (openingLiability.Amount < 0m)
            throw new ArgumentOutOfRangeException(nameof(openingLiability), "Opening lease liability cannot be negative.");

        if (effectivePeriodicRate < 0m)
            throw new ArgumentOutOfRangeException(nameof(effectivePeriodicRate), "Effective periodic rate cannot be negative.");

        if (payment.Amount < 0m)
            throw new ArgumentOutOfRangeException(nameof(payment), "Lease payment cannot be negative.");

        var currency = openingLiability.CurrencyInfo;
        var interestAmount = checked(openingLiability.Amount * effectivePeriodicRate);
        var interest = Money.CreateRounded(
            interestAmount,
            currency,
            MidpointRounding.AwayFromZero);

        var principalReduction = payment - interest;
        var closingLiability = openingLiability - principalReduction;

        if (closingLiability.Amount < 0m)
            throw new ArgumentException(
                "Lease payment cannot exceed the opening liability plus accrued interest.",
                nameof(payment));

        return new LeasePaymentCalculation(
            openingLiability,
            payment,
            interest,
            principalReduction,
            closingLiability);
    }
}
