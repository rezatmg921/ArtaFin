namespace ArtaFin.Application.Features.Accounts.CreateAccount;

/// <summary>
/// Indicates that the requested FinancialBook does not exist or is not accessible
/// to the authenticated user. The same result avoids disclosing another user's books.
/// </summary>
public sealed class FinancialBookAccessDeniedException()
    : Exception("The financial book was not found or is not accessible to the current user.");

/// <summary>
/// Indicates that an account code is already in use within the FinancialBook.
/// The code is optional because a database race can be detected after the insert
/// attempt, when the conflicting request's value is not available to the translator.
/// </summary>
public sealed class AccountCodeAlreadyExistsException : Exception
{
    public AccountCodeAlreadyExistsException(string normalizedCode)
        : base($"Account code '{normalizedCode}' already exists in this financial book.")
    {
    }

    public AccountCodeAlreadyExistsException()
        : base("An account with this code already exists in the financial book.")
    {
    }
}
