using ArtaFin.Application.Features.Accounts.CreateAccount;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

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

            FinancialBookAccessDeniedException =>
                (StatusCodes.Status404NotFound,
                    "Financial book not found",
                    "The financial book was not found or is not accessible."),

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
