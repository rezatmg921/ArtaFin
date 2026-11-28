namespace ArtaFin.Application.Features.ObligationPayments;

public sealed class FinancialObligationPaymentAlreadyReversedException()
    : Exception("The obligation payment has already been reversed.");
