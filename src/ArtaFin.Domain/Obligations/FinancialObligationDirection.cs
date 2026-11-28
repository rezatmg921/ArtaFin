namespace ArtaFin.Domain.Obligations;

/// <summary>Identifies which party owes the contractual amount.</summary>
public enum FinancialObligationDirection : byte
{
    /// <summary>The counterparty owes money to the financial book.</summary>
    Receivable = 1,

    /// <summary>The financial book owes money to the counterparty.</summary>
    Payable = 2
}
