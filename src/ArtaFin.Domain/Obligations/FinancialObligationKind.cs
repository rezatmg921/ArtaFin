namespace ArtaFin.Domain.Obligations;

/// <summary>Identifies the business origin of an obligation.</summary>
public enum FinancialObligationKind : byte
{
    GeneralDebt = 1,
    Loan = 2
}
