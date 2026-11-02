using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OrganizationRequests.Application;
using OrganizationRequests.Application.Abstractions;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Application.Repositories;
using OrganizationRequests.Application.Services;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;
using OrganizationRequests.Domain.UnitOfWork;
using OrganizationRequests.Infrastructure;
using OrganizationRequests.Infrastructure.Persistence;
using Xunit;

namespace OrganizationRequests.Tests.Integration;

[Trait("Category", "SqlServer")]
[Trait("Category", "Concurrency")]
public sealed class RequestHistorySqlTests
{
    [HistorySqlFact]
    public async Task Committed_events_are_atomic_idempotent_and_queryable_across_concurrent_operations()
    {
        var connection = Environment.GetEnvironmentVariable("ORGANIZATIONREQUESTS_HISTORY_TEST_SQL")!;
        Assert.StartsWith("OrganizationRequests_Test_", new SqlConnectionStringBuilder(connection).InitialCatalog, StringComparison.Ordinal);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = connection }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        var testUser = new TestUser();
        services.AddSingleton<ICurrentUserContext>(testUser);
        services.AddApplication();
        services.AddInfrastructure(config);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        int expertId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.False(await db.Experts.AnyAsync());
            Assert.False(await db.ServiceRequests.AnyAsync());
            Assert.False(await db.RequestActivityHistories.AnyAsync());
            var expert = new Expert { Name = "History test", IsActive = true, Capacity = 2,
                Specialties = [new ExpertSpecialty { Subject = RequestSubject.Technical }, new ExpertSpecialty { Subject = RequestSubject.Legal }] };
            db.Experts.Add(expert);
            await db.SaveChangesAsync();
            expertId = expert.Id;
            testUser.ExpertId = expertId;
        }
        var input = new RegisterRequestDto { RequestKey = Guid.NewGuid(), Subject = RequestSubject.Technical, Priority = RequestPriority.Normal };
        async Task<RegisterRequestResult> Register()
        {
            await using var scope = provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IRequestRegistrationService>().RegisterAsync(input);
        }
        var registrations = await Task.WhenAll(Register(), Register());
        Assert.Single(registrations, x => x.IsNewlyCreated);
        var requestId = registrations[0].Request.Id;
        Assert.All(registrations, x => Assert.Equal(requestId, x.Request.Id));

        // History insertion failure happens after the expert and conditional request updates.
        await using (var scope = provider.CreateAsyncScope())
        {
            var sp = scope.ServiceProvider;
            var brokenHistory = new RequestHistoryService(new FailingHistoryRepository());
            var service = new RequestAssignmentService(sp.GetRequiredService<IServiceRequestRepository>(), sp.GetRequiredService<IExpertRepository>(),
                sp.GetRequiredService<IExpertSelectionService>(), sp.GetRequiredService<IUnitOfWork>(), sp.GetRequiredService<IPersistenceErrorClassifier>(),
                brokenHistory, NullLogger<RequestAssignmentService>.Instance);
            await Assert.ThrowsAsync<InjectedHistoryException>(() => service.TryAssignAsync(requestId));
        }
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(RequestStatus.PendingAssignment, (await db.ServiceRequests.SingleAsync(x => x.Id == requestId)).Status);
            Assert.Equal(0, (await db.Experts.SingleAsync(x => x.Id == expertId)).ActiveRequestCount);
            Assert.Single(await db.RequestActivityHistories.ToListAsync());
        }
        async Task<AssignmentResult> Assign()
        {
            await using var scope = provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IRequestAssignmentService>().TryAssignAsync(requestId);
        }
        var assignments = await Task.WhenAll(Assign(), Assign());
        Assert.Single(assignments, x => x.Status == AssignmentResultStatus.Assigned);
        // A failure while recording closure must undo both the status update and capacity change.
        await using (var scope = provider.CreateAsyncScope())
        {
            var sp = scope.ServiceProvider;
            var service = new RequestClosureService(sp.GetRequiredService<IServiceRequestRepository>(), sp.GetRequiredService<IExpertRepository>(),
                sp.GetRequiredService<IUnitOfWork>(), sp.GetRequiredService<IPersistenceErrorClassifier>(), sp.GetRequiredService<ICurrentUserContext>(),
                new RequestHistoryService(new FailingHistoryRepository()), NullLogger<RequestClosureService>.Instance);
            await Assert.ThrowsAsync<InjectedHistoryException>(() => service.CloseAsync(requestId));
        }
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(RequestStatus.Assigned, (await db.ServiceRequests.SingleAsync(x => x.Id == requestId)).Status);
            Assert.Equal(1, (await db.Experts.SingleAsync(x => x.Id == expertId)).ActiveRequestCount);
            Assert.Equal(2, await db.RequestActivityHistories.CountAsync());
        }
        async Task<RequestClosureResult> Close()
        {
            await using var scope = provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IRequestClosureService>().CloseAsync(requestId);
        }
        var closures = await Task.WhenAll(Close(), Close());
        Assert.Single(closures, x => x.Status == RequestClosureResultStatus.Closed);
        await using (var scope = provider.CreateAsyncScope())
        {
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var request = await db.ServiceRequests.AsNoTracking().SingleAsync(x => x.Id == requestId);
            var events = await db.RequestActivityHistories.AsNoTracking().Where(x => x.ServiceRequestId == requestId).OrderBy(x => x.Id).ToListAsync();
            Assert.Equal(3, events.Count);
            Assert.Equal(new[] { RequestActivityType.Registered, RequestActivityType.Assigned, RequestActivityType.Closed }, events.Select(x => x.EventType));
            Assert.Equal(request.CreatedOn, events[0].OccurredAtUtc);
            Assert.Equal(request.AssignedAt, events[1].OccurredAtUtc);
            Assert.Equal(request.ClosedAt, events[2].OccurredAtUtc);
            Assert.Equal(expertId, events[2].ExpertId);
            Assert.Equal(42, events[2].ActorUserId);
            Assert.Null(events[1].ActorUserId);
            Assert.Equal(0, (await db.Experts.SingleAsync(x => x.Id == expertId)).ActiveRequestCount);
            await db.Experts.Where(x => x.Id == expertId).ExecuteUpdateAsync(x => x.SetProperty(e => e.IsActive, false));
            var history = sp.GetRequiredService<IRequestHistoryService>();
            var page1 = await history.GetExpertHistoryAsync(expertId, null, 1);
            var page2 = await history.GetExpertHistoryAsync(expertId, page1.NextCursor, 1);
            Assert.Equal(2, page1.Items.Count + page2.Items.Count);
            Assert.NotEqual(page1.Items[0].Id, page2.Items[0].Id);
            Assert.Equal(RequestActivityType.Closed, page1.Items[0].EventType);
            Assert.Equal(RequestActivityType.Assigned, page2.Items[0].EventType);
        }
    }

    private sealed class TestUser : ICurrentUserContext { public int? UserId => 42; public int? ExpertId { get; set; } }
    private sealed class InjectedHistoryException : Exception;
    private sealed class FailingHistoryRepository : IRequestActivityHistoryRepository
    {
        public void DiscardPending(int requestId, RequestActivityType eventType) { }
        public Task AddAsync(RequestActivityHistory history, CancellationToken cancellationToken = default) => throw new InjectedHistoryException();
        public Task<IReadOnlyList<RequestActivityHistory>> GetByRequestIdAsync(int requestId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<RequestActivityHistory> Items, bool HasMore)> GetByExpertIdAsync(int expertId, ExpertHistoryCursor? cursor, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

public sealed class HistorySqlFactAttribute : FactAttribute
{
    public HistorySqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ORGANIZATIONREQUESTS_HISTORY_TEST_SQL")))
            Skip = "Set ORGANIZATIONREQUESTS_HISTORY_TEST_SQL to a dedicated empty migrated test database.";
    }
}
