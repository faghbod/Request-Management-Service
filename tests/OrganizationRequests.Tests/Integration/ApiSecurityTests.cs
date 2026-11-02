using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Application.Repositories;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;
using Xunit;

namespace OrganizationRequests.Tests.Integration;

[Trait("Category", "Unit")]
public sealed class ApiSecurityTests
{
    // Synthetic test key; never used by the application outside this in-memory host.
    private const string Key = "test-only-0123456789-abcdefghijklmnopqrstuvwxyz-0123456789";
    private const string Issuer = "test-issuer";
    private const string Audience = "test-audience";

    [Fact]
    public async Task Authentication_rejects_missing_invalid_expired_issuer_and_audience_tokens()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/requests/1")).StatusCode);
        foreach (var token in new[]
        {
            Token(key: "wrong-test-key-0123456789-abcdefghijklmnopqrstuvwxyz-0123"),
            Token(expires: DateTime.UtcNow.AddMinutes(-2)),
            Token(issuer: "wrong-issuer"),
            Token(audience: "wrong-audience")
        })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/requests/1");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
        }
    }

    [Fact]
    public async Task Valid_token_can_read_own_request_but_not_another_users_or_close_it()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/requests/1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/requests/2")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/requests/1/close", null)).StatusCode);
    }

    [Fact]
    public async Task Unexpected_exceptions_are_safe_and_logged_with_trace_context()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());
        foreach (var id in new[] { 3, 4 })
        {
            var response = await client.GetAsync($"/api/requests/{id}");
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.DoesNotContain("sensitive-internal-detail", body);
            Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("traceId", body);
        }
        var events = factory.Logs.Events.Where(x => x.Level == LogLevel.Error && x.Exception is not null).ToArray();
        Assert.Contains(events, x => x.Message.Contains("GET") && x.Message.Contains("/api/requests/3") && x.Message.Contains("trace"));
        Assert.Contains(events, x => x.Exception is InvalidOperationException);
    }

    [Fact]
    public async Task Explicit_conflict_stays_409_and_input_errors_do_not_echo_messages()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());
        var response = await client.PostAsJsonAsync("/api/requests", new RegisterRequestDto
        { RequestKey = Guid.NewGuid(), Subject = RequestSubject.Legal, Priority = RequestPriority.Normal });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var invalid = await client.GetAsync("/api/requests/5");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.DoesNotContain("sensitive-internal-detail", await invalid.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("", Issuer, Audience)]
    [InlineData("short", Issuer, Audience)]
    [InlineData(Key, "", Audience)]
    [InlineData(Key, Issuer, "")]
    public void Missing_or_invalid_security_settings_fail_startup(string key, string issuer, string audience)
    {
        using var factory = new ApiFactory(key, issuer, audience);
        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
    }

    private static string Token(string? key = null, string issuer = Issuer, string audience = Audience, DateTime? expires = null)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key ?? Key));
        var token = new JwtSecurityToken(issuer, audience, [new Claim("user_id", "1")],
            DateTime.UtcNow.AddMinutes(-10), expires ?? DateTime.UtcNow.AddMinutes(10),
            new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class ApiFactory(string key = Key, string issuer = Issuer, string audience = Audience) : WebApplicationFactory<OrganizationRequests.Api.Security.ApiExceptionMiddleware>
    {
        public CaptureLogs Logs { get; } = new();
        protected override IHost CreateHost(IHostBuilder builder)
        {
            // Program validates configuration before the web-host callbacks are applied.
            var settings = new Dictionary<string, string?>
            {
                ["Jwt__Key"] = key, ["Jwt__Issuer"] = issuer, ["Jwt__Audience"] = audience,
                ["ConnectionStrings__DefaultConnection"] = "Server=localhost;Database=UnusedSecurityTest;Integrated Security=True;TrustServerCertificate=True",
                ["ASPNETCORE_ENVIRONMENT"] = "Production", ["DOTNET_ENVIRONMENT"] = "Production"
            };
            var previous = settings.Keys.ToDictionary(name => name, Environment.GetEnvironmentVariable);
            try
            {
                foreach (var setting in settings) Environment.SetEnvironmentVariable(setting.Key, setting.Value);
                return base.CreateHost(builder);
            }
            finally
            {
                foreach (var setting in previous) Environment.SetEnvironmentVariable(setting.Key, setting.Value);
            }
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureLogging(logging => { logging.ClearProviders(); logging.AddProvider(Logs); });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IServiceRequestRepository>();
                services.AddScoped<IServiceRequestRepository, RequestRepository>();
            });
        }
    }

    private sealed class CaptureLogs : ILoggerProvider
    {
        public ConcurrentQueue<(LogLevel Level, Exception? Exception, string Message)> Events { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Logger(this);
        public void Dispose() { }
        private sealed class Logger(CaptureLogs owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                owner.Events.Enqueue((logLevel, exception, formatter(state, exception)));
        }
    }

    private sealed class RequestRepository : IServiceRequestRepository
    {
        public Task<ServiceRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            if (id == 3) throw new InvalidOperationException("sensitive-internal-detail");
            if (id == 4) throw new Exception("sensitive-internal-detail");
            if (id == 5) throw new ArgumentException("sensitive-internal-detail");
            return Task.FromResult<ServiceRequest?>(id is 1 or 2 ? Make(id, id) : null);
        }
        public Task<ServiceRequest?> GetByRequestKeyAsync(Guid key, CancellationToken cancellationToken = default) =>
            Task.FromResult<ServiceRequest?>(Make(1, 1));
        private static ServiceRequest Make(int id, int owner) => new() { Id = id, CreatedBy = owner, RequestKey = Guid.NewGuid(),
            Subject = RequestSubject.Technical, Priority = RequestPriority.Normal, Status = RequestStatus.PendingAssignment, CreatedOn = DateTime.UtcNow };
        public Task AddAsync(ServiceRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void DiscardAdded(ServiceRequest request) => throw new NotSupportedException();
        public Task<ServiceRequest?> GetForAssignmentAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> TryMarkClosedAsync(int requestId, int expertId, DateTime closedAtUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> TryMarkAssignedAsync(int requestId, int expertId, DateTime assignedAtUtc, RequestSubject subject, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PendingRequestPage> GetPendingPageAsync(DateTime scan, DateTime cutoff, int size, PendingQueueCursor? cursor, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
