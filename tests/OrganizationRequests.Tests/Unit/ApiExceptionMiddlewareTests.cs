using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using OrganizationRequests.Api.Security;
using Xunit;

namespace OrganizationRequests.Tests.Unit;

// بررسی تبدیل خطاها و لغو درخواست در لایه API.
[Trait("Category", "Unit")]
public sealed class ApiExceptionMiddlewareTests
{
    [Fact]
    public async Task Client_cancellation_does_not_write_an_error_response()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new DefaultHttpContext { RequestAborted = cancellation.Token };
        context.Response.Body = new MemoryStream();
        var middleware = new ApiExceptionMiddleware(_ => throw new OperationCanceledException(cancellation.Token),
            NullLogger<ApiExceptionMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        Assert.Equal(0, context.Response.Body.Length);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task Unrelated_cancellation_is_a_server_error()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ApiExceptionMiddleware(_ => throw new OperationCanceledException(),
            NullLogger<ApiExceptionMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
    }
}