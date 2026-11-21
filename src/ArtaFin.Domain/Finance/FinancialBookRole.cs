namespace ArtaFin.Domain.Finance;

/// <summary>
/// Permissions granted to a non-owner member within one financial book.
/// Ownership is represented by FinancialBook.OwnerId, not by this enum.
/// </summary>
public enum FinancialBookRole
{
    Accountant = 1,
    Auditor = 2,
    Viewer = 3
}
