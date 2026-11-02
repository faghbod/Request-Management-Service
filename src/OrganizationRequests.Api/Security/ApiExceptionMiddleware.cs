using Microsoft.EntityFrameworkCore;
using OrganizationRequests.Application.Exceptions;

namespace OrganizationRequests.Api.Security;

public sealed class ApiExceptionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            var (statusCode, message) = exception switch
            {
                ResourceNotFoundException => (StatusCodes.Status404NotFound, exception.Message),
                AccessDeniedException => (StatusCodes.Status403Forbidden, exception.Message),
                ArgumentException => (StatusCodes.Status400BadRequest, exception.Message),
                InvalidOperationException => (StatusCodes.Status409Conflict, exception.Message),
                DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "اطلاعات هم‌زمان توسط کاربر دیگری تغییر کرده است."),
                _ => (StatusCodes.Status500InternalServerError, "خطای داخلی سرور رخ داد.")
            };

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsJsonAsync(new { message });
        }
    }
}

