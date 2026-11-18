using System.Collections.ObjectModel;

namespace ArtaFin.Domain.Finance;

/// <summary>
/// One dated contractual cash payment. The effective rate is the rate applicable
/// to the interval ending on this payment date; callers must derive it from the
/// executed contract's day-count and stub-period terms.
/// </summary>
public sealed record LeaseScheduledPayment(
    DateOnly PaymentDate,
    decimal PaymentAmount,
    decimal EffectivePeriodicRate);

/// <summary>
/// Calculated, read-only result for one dated payment period.
/// </summary>
public sealed record LeasePaymentPeriodResult(
    int PeriodNumber,
    DateOnly PaymentDate,
    decimal EffectivePeriodicRate,
    LeasePaymentCalculation Calculation);

/// <summary>
/// Builds a dated effective-interest schedule from explicit contractual payments.
/// It deliberately does not infer dates, annual-to-periodic rate conversions,
/// day-count conventions, or stub-period rates.
/// </summary>
public static class LeasePaymentScheduleCalculator
{
    public static IReadOnlyList<LeasePaymentPeriodResult> Calculate(
        Money initialLiability,
        DateOnly firstPaymentDate,
        DateOnly contractEndDate,
        IReadOnlyList<LeaseScheduledPayment> payments)
    {
        ArgumentNullException.ThrowIfNull(payments);

        if (initialLiability.Amount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(initialLiability), "Initial liability must be positive.");
        if (firstPaymentDate == default || contractEndDate < firstPaymentDate)
            throw new ArgumentException("The payment date range is invalid.");
        if (payments.Count == 0)
            throw new ArgumentException("At least one scheduled payment is required.", nameof(payments));

        var results = new List<LeasePaymentPeriodResult>(payments.Count);
        var openingLiability = initialLiability;
        DateOnly? previousPaymentDate = null;

        for (var index = 0; index < payments.Count; index++)
        {
            var payment = payments[index] ??
                throw new ArgumentException("Scheduled payments cannot contain null entries.", nameof(payments));

            if (payment.PaymentDate < firstPaymentDate || payment.PaymentDate > contractEndDate)
                throw new ArgumentException(
                    $"Payment date at period {index + 1} must be within the contract payment date range.",
                    nameof(payments));

            if (previousPaymentDate.HasValue && payment.PaymentDate <= previousPaymentDate.Value)
                throw new ArgumentException("Scheduled payment dates must be strictly increasing.", nameof(payments));

            if (payment.PaymentAmount < 0m)
                throw new ArgumentOutOfRangeException(nameof(payments), "Scheduled payment amounts cannot be negative.");

            if (payment.EffectivePeriodicRate < 0m ||
                payment.EffectivePeriodicRate > LeaseContract.MaxEffectivePeriodicRate)
                throw new ArgumentOutOfRangeException(
                    nameof(payments),
                    "Each period's effective rate must be non-negative and fit decimal(19,10).");

            var cashPayment = new Money(payment.PaymentAmount, initialLiability.Currency);
            var calculation = LeasePaymentCalculation.Calculate(
                openingLiability,
                payment.EffectivePeriodicRate,
                cashPayment);

            results.Add(new LeasePaymentPeriodResult(
                index + 1,
                payment.PaymentDate,
                payment.EffectivePeriodicRate,
                calculation));

            openingLiability = calculation.ClosingLiability;
            previousPaymentDate = payment.PaymentDate;
        }

        return new ReadOnlyCollection<LeasePaymentPeriodResult>(results);
    }
}
