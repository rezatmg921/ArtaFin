using ArtaFin.Application.Abstractions.Persistence;

namespace ArtaFin.Application.Features.Reports.GetBalanceSheet;

/// <summary>
/// Splits the unpaid portion of an obligation's installments by their due date.
/// The caller must provide only active (not reversed) payment allocations.
/// </summary>
public static class ObligationMaturityCalculator
{
    public static ObligationMaturityResult Calculate(
        DateOnly asOf,
        IReadOnlyCollection<ObligationInstallmentTerm> installments,
        IReadOnlyCollection<ObligationInstallmentPayment> payments)
    {
        ArgumentNullException.ThrowIfNull(installments);
        ArgumentNullException.ThrowIfNull(payments);

        var paidByInstallment = payments
            .GroupBy(payment => payment.InstallmentId)
            .ToDictionary(group => group.Key, group => group.Sum(payment => payment.Amount));

        var currentThrough = asOf.AddYears(1);
        decimal current = 0m;
        decimal nonCurrent = 0m;

        foreach (var installment in installments)
        {
            if (installment.InstallmentId == Guid.Empty)
                throw new ArgumentException("Installment id cannot be empty.", nameof(installments));
            if (installment.Amount <= 0m)
                throw new ArgumentOutOfRangeException(nameof(installments), "Installment amount must be greater than zero.");

            var paid = paidByInstallment.GetValueOrDefault(installment.InstallmentId);
            if (paid < 0m || paid > installment.Amount)
                throw new ArgumentException("Payments for an installment cannot exceed its amount.", nameof(payments));

            var remaining = installment.Amount - paid;
            if (installment.DueDate <= currentThrough)
                current += remaining;
            else
                nonCurrent += remaining;
        }

        return new ObligationMaturityResult(current, nonCurrent);
    }
}

public sealed record ObligationMaturityResult(decimal CurrentAmount, decimal NonCurrentAmount)
{
    public decimal TotalOutstanding => CurrentAmount + NonCurrentAmount;
}
