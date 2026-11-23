namespace ArtaFin.Domain.Security;

/// <summary>
/// The supported system permission catalog. Codes are stable authorization identifiers;
/// administrators may assign them to roles but cannot invent new codes at runtime.
/// </summary>
public static class PermissionCatalog
{
    public static IReadOnlyList<PermissionDefinition> All { get; } =
    [
        new("Users.Read", "Read users", "View user accounts and their system roles."),
        new("Users.Manage", "Manage users", "Manage user activation and role assignments."),
        new("Roles.Read", "Read roles", "View system roles and their permission grants."),
        new("Roles.Manage", "Manage roles", "Create and manage custom roles and permission grants."),
        new("FinancialBooks.Read", "Read financial books", "View financial books the user is independently allowed to access."),
        new("FinancialBooks.Create", "Create financial books", "Create a financial book."),
        new("FinancialBooks.ManageMembers", "Manage financial book members", "Manage membership for financial books the user owns or administers."),
        new("Contacts.Read", "Read contacts", "View counterparties in financial books the user may access."),
        new("Contacts.Create", "Create contacts", "Create counterparties in financial books the user may access."),
        new("Contacts.Update", "Update contacts", "Update counterparties in financial books the user may access."),
        new("Contacts.Archive", "Archive contacts", "Archive counterparties without deleting their financial history."),
        new("Obligations.Read", "Read financial obligations", "View receivables and payables in financial books the user may access."),
        new("Obligations.Create", "Create financial obligations", "Create receivables and payables in financial books the user may access."),
        new("Obligations.Cancel", "Cancel financial obligations", "Cancel eligible unsettled obligations in financial books the user may access."),
        new("ObligationPayments.Read", "Read obligation payments", "View posted receipts and disbursements for obligations in financial books the user may access."),
        new("ObligationPayments.Create", "Create obligation payments", "Post receipts and disbursements against obligation installments in financial books the user may access."),
        new("Accounts.Read", "Read accounts", "View accounts in financial books the user may access."),
        new("Accounts.Create", "Create accounts", "Create accounts in financial books the user may access."),
        new("Accounts.Update", "Update accounts", "Update accounts in financial books the user may access."),
        new("Accounts.Delete", "Delete accounts", "Delete eligible accounts in financial books the user may access."),
        new("Transactions.Read", "Read transactions", "View transactions in financial books the user may access."),
        new("Transactions.Create", "Create transactions", "Create transactions in financial books the user may access."),
        new("Transactions.Reverse", "Reverse transactions", "Reverse eligible transactions in financial books the user may access."),
        new("Transactions.Approve", "Approve transactions", "Approve transactions where approval workflows are enabled."),
        new("Leases.Read", "Read leases", "View lease contracts in financial books the user may access."),
        new("Leases.Manage", "Manage leases", "Manage lease contracts in financial books the user may access."),
        new("Installments.Pay", "Pay installments", "Record installment payments in financial books the user may access."),
        new("Reports.Read", "Read reports", "View reports for financial books the user may access.")
    ];
}

public sealed record PermissionDefinition(string Code, string Name, string Description);
