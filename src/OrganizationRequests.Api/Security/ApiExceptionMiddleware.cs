using Microsoft.EntityFrameworkCore;
using OrganizationRequests.Application.Exceptions;

namespace OrganizationRequests.Api.Security;

// تبدیل خطاهای برنامه به پاسخ HTTP مناسب.
public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // اتصال کلاینت قطع شده است؛ برای نوشتن پاسخ خطا تلاش نمی‌کنیم.
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            var (statusCode, message) = exception switch
            {
                ResourceNotFoundException => (StatusCodes.Status404NotFound, "Resource not found."),
                AccessDeniedException => (StatusCodes.Status403Forbidden, "Access denied."),
                RequestKeyConflictException => (StatusCodes.Status409Conflict, "The request key cannot be used for this registration."),
                ExpertValidationException => (StatusCodes.Status400BadRequest, "Invalid expert input."),
                ExpertConcurrencyException => (StatusCodes.Status409Conflict, "The expert was modified concurrently."),
                RequestValidationException => (StatusCodes.Status400BadRequest, "Invalid request."),
                DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "The resource was modified concurrently."),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected server error occurred.")
            };

            if (statusCode == StatusCodes.Status500InternalServerError)
                logger.LogError(exception, "Unhandled exception during {Method} {Path}; trace {TraceId}.",
                    context.Request.Method, context.Request.Path.Value, context.TraceIdentifier);

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsJsonAsync(new { message, traceId = context.TraceIdentifier });
        }
    }
}

