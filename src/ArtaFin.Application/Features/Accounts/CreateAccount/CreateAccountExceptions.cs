namespace ArtaFin.Application.Features.Accounts.CreateAccount;

/// <summary>
/// Indicates that the requested FinancialBook does not exist or is not accessible
/// to the authenticated user. The same result avoids disclosing another user's books.
/// </summary>
public sealed class FinancialBookAccessDeniedException()
    : Exception("The financial book was not found or is not accessible to the current user.");

/// <summary>
/// Indicates that an account code is already in use within the FinancialBook.
/// </summary>
public sealed class AccountCodeAlreadyExistsException(string normalizedCode)
    : Exception($"Account code '{normalizedCode}' already exists in this financial book.");
