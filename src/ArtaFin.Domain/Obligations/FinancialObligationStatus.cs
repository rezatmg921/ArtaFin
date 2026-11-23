namespace ArtaFin.Domain.Obligations;

/// <summary>
/// Current settlement lifecycle state. Overdue is derived from DueDate and the
/// current date; it is deliberately not persisted as a mutable status.
/// </summary>
public enum FinancialObligationStatus : byte
{
    Open = 1,
    PartiallySettled = 2,
    Settled = 3,
    Cancelled = 4
}
