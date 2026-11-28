namespace ArtaFin.Application.Features.Expenses.CreateExpense;

public sealed class ExpenseAccountsMustBeDifferentException()
    : Exception("Expense and payment accounts must be different.");

public sealed class ExpenseAccountTypeInvalidException()
    : Exception("The selected expense account must have the Expense account type.");

public sealed class ExpensePaymentAccountTypeInvalidException()
    : Exception("The payment account must have the Asset account type.");
