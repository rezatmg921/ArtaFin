namespace ArtaFin.Domain.Finance;

/// <summary>
/// Calculates the effective-interest roll-forward for each dated contractual
/// payment. Rates are supplied per interval; this type does not infer annual
/// conversions, day-count conventions, or payment timing.
/// </summary>
public static class LeasePaymentScheduleCalculator
{
    public static IReadOnlyList<LeasePaymentSchedulePeriodCalculation> Calculate(
        Money openingLiability,
        DateOnly firstPaymentDate,
        DateOnly contractEndDate,
        IReadOnlyList<LeaseScheduledPayment> payments)
    {
        ArgumentNullException.ThrowIfNull(payments);

        if (openingLiability.Amount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(openingLiability),
                "Opening lease liability must be positive.");
        if (contractEndDate < firstPaymentDate)
            throw new ArgumentException("Contract end date cannot precede the first payment date.");
        if (payments.Count is < 1 or > LeasePaymentSchedule.MaxPayments)
            throw new ArgumentOutOfRangeException(nameof(payments),
                $"A schedule must contain between 1 and {LeasePaymentSchedule.MaxPayments} payments.");

        var results = new List<LeasePaymentSchedulePeriodCalculation>(payments.Count);
        var liability = openingLiability;
        DateOnly? previousPaymentDate = null;

        for (var index = 0; index < payments.Count; index++)
        {
            var payment = payments[index] ??
                throw new ArgumentException("Payment rows cannot contain null values.", nameof(payments));

            if (payment.PaymentDate < firstPaymentDate ||
                payment.PaymentDate > contractEndDate ||
                (previousPaymentDate.HasValue && payment.PaymentDate <= previousPaymentDate.Value))
            {
                throw new ArgumentException(
                    "Payment dates must be on or after the first payment date, no later than the contract end date, and strictly increasing.",
                    nameof(payments));
            }

            if (payment.PaymentAmount <= 0m)
                throw new ArgumentOutOfRangeException(nameof(payments),
                    "Contractual payment amounts must be positive.");
            if (payment.EffectivePeriodicRate < 0m ||
                payment.EffectivePeriodicRate > LeaseContract.MaxEffectivePeriodicRate)
                throw new ArgumentOutOfRangeException(nameof(payments),
                    "Effective periodic rates must be non-negative and fit decimal(19,10).");

            var moneyPayment = new Money(payment.PaymentAmount, openingLiability.Currency);
            var calculation = LeasePaymentCalculation.Calculate(
                liability, payment.EffectivePeriodicRate, moneyPayment);

            results.Add(new LeasePaymentSchedulePeriodCalculation(
                index + 1, payment.PaymentDate, calculation));
            liability = calculation.ClosingLiability;
            previousPaymentDate = payment.PaymentDate;
        }

        return results;
    }
}

/// <summary>A single dated contractual payment and its effective rate for that interval.</summary>
public sealed record LeaseScheduledPayment(
    DateOnly PaymentDate,
    decimal PaymentAmount,
    decimal EffectivePeriodicRate);

/// <summary>Calculated liability movement for one schedule period.</summary>
public sealed record LeasePaymentSchedulePeriodCalculation(
    int PeriodNumber,
    DateOnly PaymentDate,
    LeasePaymentCalculation Calculation);
