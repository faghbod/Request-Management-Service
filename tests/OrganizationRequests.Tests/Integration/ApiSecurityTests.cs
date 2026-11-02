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
using OrganizationRequests.Application.Services;
using OrganizationRequests.Application.Exceptions;
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
    public async Task Request_history_requires_authentication_and_request_ownership()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/requests/1/history")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/requests/1/history")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/requests/2/history")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/experts/1/history")).StatusCode);
    }
    [Fact]
    public async Task Signed_expert_claim_grants_request_and_history_access_only_when_trusted()
    {
        await using var trusted = new ApiFactory(trustedExpertClaim: true);
        using var client = trusted.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(expertId: 7));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/requests/2")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/requests/2/history")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(expertId: 8));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/requests/2")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/requests/2/history")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(key: "wrong-test-key-0123456789-abcdefghijklmnopqrstuvwxyz-0123", expertId: 7));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/requests/2")).StatusCode);

        await using var untrusted = new ApiFactory();
        using var denied = untrusted.CreateClient();
        denied.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(expertId: 7));
        Assert.Equal(HttpStatusCode.NotFound, (await denied.GetAsync("/api/requests/2")).StatusCode);
    }
    [Fact]
    public async Task Expert_management_requires_a_trusted_explicit_permission()
    {
        await using var factory = new ApiFactory(trustedAdminClaim: true);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/experts/7")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(expertId: 7));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/experts/7")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/experts", new { name = "A", capacity = 3, isActive = true, specialties = new[] { 1 } })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(permission: "experts.manage"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/experts/7")).StatusCode);
        Assert.Equal(1, factory.Management.Calls);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/experts?pageSize=10&isActive=true&subject=1")).StatusCode);
        var created = await client.PostAsJsonAsync("/api/experts", new { name = "A", capacity = 3, isActive = true, specialties = new[] { 1 } });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/experts/7", new { name = "A", capacity = 3,
            isActive = true, specialties = new[] { 1 }, expectedRowVersion = "AQIDBAUGBwg=" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync("/api/experts/7/status", new { isActive = false,
            expectedRowVersion = "AQIDBAUGBwg=" })).StatusCode);
        Assert.Equal(5, factory.Management.Calls);
        var forged = await client.PostAsJsonAsync("/api/experts", new { name = "A", capacity = 3, isActive = true,
            specialties = new[] { 1 }, activeRequestCount = 2 });
        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        Assert.Equal(5, factory.Management.Calls);
        factory.Management.Conflict = true;
        Assert.Equal(HttpStatusCode.Conflict, (await client.GetAsync("/api/experts/7")).StatusCode);

        await using var untrusted = new ApiFactory();
        using var blocked = untrusted.CreateClient();
        blocked.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(permission: "experts.manage"));
        Assert.Equal(HttpStatusCode.Forbidden, (await blocked.GetAsync("/api/experts/7")).StatusCode);
        Assert.Equal(0, untrusted.Management.Calls);
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

    private static string Token(string? key = null, string issuer = Issuer, string audience = Audience, DateTime? expires = null, int? expertId = null, string? permission = null)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key ?? Key));
        var claims = new List<Claim> { new("user_id", "1") };
        if (expertId is not null) claims.Add(new Claim("expert_id", expertId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        if (permission is not null) claims.Add(new Claim("permission", permission));
        var token = new JwtSecurityToken(issuer, audience, claims,
            DateTime.UtcNow.AddMinutes(-10), expires ?? DateTime.UtcNow.AddMinutes(10),
            new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class ApiFactory(string key = Key, string issuer = Issuer, string audience = Audience, bool trustedExpertClaim = false, bool trustedAdminClaim = false) : WebApplicationFactory<OrganizationRequests.Api.Security.ApiExceptionMiddleware>
    {
        public CaptureLogs Logs { get; } = new();
        public FakeExpertManagement Management { get; } = new();
        protected override IHost CreateHost(IHostBuilder builder)
        {
            // Program validates configuration before the web-host callbacks are applied.
            var settings = new Dictionary<string, string?>
            {
                ["Jwt__Key"] = key, ["Jwt__Issuer"] = issuer, ["Jwt__Audience"] = audience,
                ["Jwt__TrustExpertIdClaim"] = trustedExpertClaim.ToString(),
                ["Jwt__TrustExpertManagementPermission"] = trustedAdminClaim.ToString(),
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
                services.RemoveAll<IRequestActivityHistoryRepository>();
                services.AddScoped<IRequestActivityHistoryRepository, TestHistoryRepository>();
                services.RemoveAll<IExpertManagementService>();
                services.AddSingleton<IExpertManagementService>(Management);
            });
        }
    }

    private sealed class FakeExpertManagement : IExpertManagementService
    {
        public int Calls;
        public bool Conflict;
        public Task<ExpertResponseDto> GetExpertByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Conflict) throw new ExpertConcurrencyException();
            return Task.FromResult(new ExpertResponseDto(id, "Test", 3, 0, 3, true, [RequestSubject.Financial], "AQIDBAUGBwg=", DateTime.UtcNow));
        }
        public Task<ExpertResponseDto> CreateExpertAsync(CreateExpertDto input, CancellationToken cancellationToken = default) => GetExpertByIdAsync(7, cancellationToken);
        public Task<ExpertManagementPage> GetExpertsAsync(int? afterId, int pageSize, bool? isActive, RequestSubject? subject, CancellationToken cancellationToken = default) { Calls++; return Task.FromResult(new ExpertManagementPage([], null)); }
        public Task<ExpertResponseDto> UpdateExpertAsync(int id, UpdateExpertDto input, CancellationToken cancellationToken = default) => GetExpertByIdAsync(id, cancellationToken);
        public Task<ExpertResponseDto> SetExpertActiveStatusAsync(int id, SetExpertActiveStatusDto input, CancellationToken cancellationToken = default) => GetExpertByIdAsync(id, cancellationToken);
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
            Subject = RequestSubject.Technical, Priority = RequestPriority.Normal, Status = RequestStatus.PendingAssignment, CreatedOn = DateTime.UtcNow,
            AssignedExpertId = id == 2 ? 7 : null };
        public Task AddAsync(ServiceRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void DiscardAdded(ServiceRequest request) => throw new NotSupportedException();
        public Task<ServiceRequest?> GetForAssignmentAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> TryMarkClosedAsync(int requestId, int expertId, DateTime closedAtUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> TryMarkAssignedAsync(int requestId, int expertId, DateTime assignedAtUtc, RequestSubject subject, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PendingRequestPage> GetPendingPageAsync(DateTime scan, DateTime cutoff, int size, PendingQueueCursor? cursor, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
