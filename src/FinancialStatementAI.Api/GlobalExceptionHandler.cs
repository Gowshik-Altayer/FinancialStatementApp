using System.Security.Claims;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Entities;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FinancialStatementAI.Api;

/// <summary>Catches every unhandled exception anywhere in the API (requirement: no raw stack
/// traces or silent failures reaching a client — mirrors the Angular error.interceptor's own
/// "never a bare unhandled failure" principle). Logs it via ILogger as usual, persists it to the
/// ExceptionLogs table so it's visible without log-file access, and always returns a generic
/// ProblemDetails response — never the exception's own message or stack trace, which could leak
/// internal details to the client.
///
/// Takes IServiceScopeFactory rather than IExceptionLogRepository directly: AddExceptionHandler
/// registers this class as a singleton, but IExceptionLogRepository (like everything backed by
/// AppDbContext) is scoped — injecting it straight into a singleton is a captive-dependency bug
/// that throws immediately in Development, where DI scope validation is on by default. Resolving
/// it from a short-lived scope per exception is the standard fix.</summary>
public class GlobalExceptionHandler(IServiceScopeFactory serviceScopeFactory, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var exceptionLogRepository = scope.ServiceProvider.GetRequiredService<IExceptionLogRepository>();
            await exceptionLogRepository.AddAsync(new ExceptionLog
            {
                ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
                Message = exception.Message,
                StackTrace = exception.StackTrace,
                RequestPath = httpContext.Request.Path,
                RequestMethod = httpContext.Request.Method,
                UserId = TryGetUserId(httpContext),
                StatusCode = StatusCodes.Status500InternalServerError
            }, cancellationToken);
        }
        catch (Exception loggingException)
        {
            // Never let a failure to persist the log entry itself (e.g. the very exception that
            // triggered this was a database outage) prevent the client from still getting a
            // response, or mask the original exception already logged via ILogger above.
            logger.LogError(loggingException, "Failed to persist exception log entry.");
        }

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Title = "An unexpected error occurred.",
            Status = StatusCodes.Status500InternalServerError,
            Detail = "The error has been logged. Please try again or contact support if the problem persists."
        }, cancellationToken);

        return true;
    }

    private static Guid? TryGetUserId(HttpContext httpContext)
    {
        var claim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var userId) ? userId : null;
    }
}
