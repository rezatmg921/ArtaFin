namespace ArtaFin.Application.Features.Income.CreateIncome;

public sealed class IncomeAccountsMustBeDifferentException()
    : Exception("Income and deposit accounts must be different.");

public sealed class IncomeAccountTypeInvalidException()
    : Exception("The selected income account must have the Income account type.");

public sealed class IncomeDepositAccountTypeInvalidException()
    : Exception("The deposit account must have the Asset account type.");
