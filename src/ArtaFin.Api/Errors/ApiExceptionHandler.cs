using ArtaFin.Application.Features.Accounts.CreateAccount;
using ArtaFin.Application.Features.FinancialBooks.InviteMember;
using ArtaFin.Application.Features.JournalEntries.CreateJournalEntry;
using ArtaFin.Application.Features.JournalEntries.ReverseJournalEntry;
using ArtaFin.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Api.Errors;

/// <summary>
/// Converts known application exceptions into stable HTTP Problem Details responses.
/// Unknown exceptions are left to the ASP.NET Core exception handler fallback.
/// </summary>
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
            AccountCodeAlreadyExistsException =>
                (StatusCodes.Status409Conflict,
                    "Account code already exists",
                    "An account with this code already exists in the financial book."),

            FinancialBookAccessDeniedException or
            FinancialBookManagementDeniedException or
            JournalEntryAccessDeniedException =>
                (StatusCodes.Status404NotFound,
                    "Financial book not found",
                    "The financial book was not found or is not accessible."),

            DbUpdateException databaseException
                when databaseException.ToString().Contains(
                    "UX_JournalEntries_ReversesJournalEntryId",
                    StringComparison.Ordinal) =>
                (StatusCodes.Status409Conflict,
                    "Journal entry already reversed",
                    "This journal entry already has a reversal entry."),

            JournalEntryAlreadyReversedException =>
                (StatusCodes.Status409Conflict,
                    "Journal entry already reversed",
                    "This journal entry already has a reversal entry."),

            DomainException =>
                (StatusCodes.Status400BadRequest,
                    "Invalid journal entry",
                    exception.Message),

            JournalEntryAccountUnavailableException =>
                (StatusCodes.Status400BadRequest,
                    "Journal entry account unavailable",
                    "One or more accounts are unavailable in this financial book."),

            InviteTargetUserNotFoundException =>
                (StatusCodes.Status404NotFound,
                    "User not found",
                    "The specified user was not found or is not active."),

            FinancialBookMemberAlreadyExistsException =>
                (StatusCodes.Status409Conflict,
                    "Membership already exists",
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
                Extensions =
                {
                    ["traceId"] = httpContext.TraceIdentifier
                }
            }
        });
    }
}
