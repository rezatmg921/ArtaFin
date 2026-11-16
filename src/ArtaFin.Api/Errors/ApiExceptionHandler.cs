using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Features.Accounts.CreateAccount;
using ArtaFin.Application.Features.Accounts.DeleteAccount;
using ArtaFin.Application.Features.Accounts.UpdateAccount;
using ArtaFin.Application.Features.FinancialBooks.InviteMember;
using ArtaFin.Application.Features.Expenses.CreateExpense;
using ArtaFin.Application.Features.Income.CreateIncome;
using ArtaFin.Application.Features.Transfers.CreateTransfer;
using ArtaFin.Application.Features.FinancialBooks.ManageMember;
using ArtaFin.Application.Features.JournalEntries.CreateJournalEntry;
using ArtaFin.Application.Features.JournalEntries.ReverseJournalEntry;
using ArtaFin.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Api.Errors;

internal sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            PersistenceConcurrencyException =>
                (StatusCodes.Status409Conflict, "Concurrency conflict",
                    "The resource was modified by another operation. Please reload and try again."),
            AccountHasJournalEntriesException =>
                (StatusCodes.Status409Conflict, "Account has accounting history",
                    "The account is referenced by journal entries. Deactivate it instead of deleting it."),
            AccountPreconditionFailedException =>
                (StatusCodes.Status412PreconditionFailed, "Account version mismatch",
                    "The account has changed since it was last retrieved. Fetch the latest version and retry."),
            AccountCodeAlreadyExistsException =>
                (StatusCodes.Status409Conflict, "Account code already exists",
                    "An account with this code already exists in the financial book."),
            FinancialBookAccessDeniedException or FinancialBookManagementDeniedException or JournalEntryAccessDeniedException =>
                (StatusCodes.Status404NotFound, "Financial book not found",
                    "The financial book was not found or is not accessible."),
            FinancialBookMemberNotFoundException =>
                (StatusCodes.Status404NotFound, "Financial book member not found",
                    "The membership was not found in this financial book."),
            FinancialBookMemberNotActiveException =>
                (StatusCodes.Status409Conflict, "Membership is not active",
                    "The membership has already been revoked and cannot be changed again."),
            DbUpdateException databaseException
                when databaseException.ToString().Contains("UX_JournalEntries_ReversesJournalEntryId", StringComparison.Ordinal) =>
                (StatusCodes.Status409Conflict, "Journal entry already reversed",
                    "This journal entry already has a reversal entry."),
            JournalEntryAlreadyReversedException =>
                (StatusCodes.Status409Conflict, "Journal entry already reversed",
                    "This journal entry already has a reversal entry."),
            DomainException =>
                (StatusCodes.Status400BadRequest, "Invalid journal entry", exception.Message),
            JournalEntryAccountUnavailableException =>
                (StatusCodes.Status400BadRequest, "Journal entry account unavailable",
                    "One or more accounts are unavailable in this financial book."),
            JournalEntryBaseCurrencyMismatchException =>
                (StatusCodes.Status400BadRequest, "Journal entry currency mismatch",
                    "Every journal line must use the financial book's base currency."),
            ExpenseAccountsMustBeDifferentException or ExpenseAccountTypeInvalidException or ExpensePaymentAccountTypeInvalidException =>
                (StatusCodes.Status400BadRequest, "Invalid expense accounts", exception.Message),
            IncomeAccountsMustBeDifferentException or IncomeAccountTypeInvalidException or IncomeDepositAccountTypeInvalidException =>
                (StatusCodes.Status400BadRequest, "Invalid income accounts", exception.Message),
            TransferAccountsMustBeDifferentException or TransferAccountTypeInvalidException =>
                (StatusCodes.Status400BadRequest, "Invalid transfer accounts", exception.Message),
            InviteTargetUserNotFoundException =>
                (StatusCodes.Status404NotFound, "User not found",
                    "The specified user was not found or is not active."),
            FinancialBookMemberAlreadyExistsException =>
                (StatusCodes.Status409Conflict, "Membership already exists",
                    "The user is already the owner or a member of this financial book."),
            _ => (0, string.Empty, string.Empty)
        };

        if (status == 0)
            return false;

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Type = "about:blank",
                Extensions = { ["traceId"] = httpContext.TraceIdentifier }
            }
        });
    }
}
