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
public sealed class AssignmentRollbackSqlTests
{
    [AssignmentFaultSqlFact]
    public async Task Failure_after_counter_update_and_zero_affected_request_both_roll_back()
    {
        var connection = Environment.GetEnvironmentVariable("ORGANIZATIONREQUESTS_ASSIGNMENT_FAULT_SQL")!;
        Assert.StartsWith("OrganizationRequests_Test_", new SqlConnectionStringBuilder(connection).InitialCatalog, StringComparison.Ordinal);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:DefaultConnection"] = connection }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ICurrentUserContext, TestUser>();
        services.AddApplication();
        services.AddInfrastructure(config);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        int requestId, expertId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Contains("20261102090017_InitialOrganizationRequests", await db.Database.GetAppliedMigrationsAsync());
            Assert.False(await db.Experts.AnyAsync());
            Assert.False(await db.ServiceRequests.AnyAsync());
            var expert = new Expert { Name = "Assignment rollback", IsActive = true, Capacity = 1,
                Specialties = [new ExpertSpecialty { Subject = RequestSubject.Technical }] };
            var request = new ServiceRequest { RequestKey = Guid.NewGuid(), Subject = RequestSubject.Technical,
                Priority = RequestPriority.Normal, Status = RequestStatus.PendingAssignment };
            db.Experts.Add(expert);
            db.ServiceRequests.Add(request);
            await db.SaveChangesAsync();
            (requestId, expertId) = (request.Id, expert.Id);
        }

        await Assert.ThrowsAsync<InjectedException>(() => Attempt(provider, requestId, fail: true));
        await AssertState(provider, requestId, expertId, RequestStatus.PendingAssignment, 0);
        Assert.Equal(AssignmentResultStatus.RetryableConflict, (await Attempt(provider, requestId, fail: false)).Status);
        await AssertState(provider, requestId, expertId, RequestStatus.PendingAssignment, 0);
        await using (var scope = provider.CreateAsyncScope())
            Assert.Equal(AssignmentResultStatus.Assigned, (await scope.ServiceProvider.GetRequiredService<IRequestAssignmentService>().TryAssignAsync(requestId)).Status);
        await AssertState(provider, requestId, expertId, RequestStatus.Assigned, 1);
        await using (var scope = provider.CreateAsyncScope())
            Assert.Equal(AssignmentResultStatus.AlreadyProcessed, (await scope.ServiceProvider.GetRequiredService<IRequestAssignmentService>().TryAssignAsync(requestId)).Status);
        await AssertState(provider, requestId, expertId, RequestStatus.Assigned, 1);
    }

    private static async Task<AssignmentResult> Attempt(ServiceProvider provider, int requestId, bool fail)
    {
        await using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var requests = new FailingRequestUpdate(sp.GetRequiredService<IServiceRequestRepository>(), fail);
        var service = new RequestAssignmentService(requests, sp.GetRequiredService<IExpertRepository>(),
            sp.GetRequiredService<IExpertSelectionService>(), sp.GetRequiredService<IUnitOfWork>(),
            sp.GetRequiredService<IPersistenceErrorClassifier>(), NullLogger<RequestAssignmentService>.Instance);
        return await service.TryAssignAsync(requestId);
    }

    private static async Task AssertState(ServiceProvider provider, int requestId, int expertId, RequestStatus status, int count)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var request = await db.ServiceRequests.AsNoTracking().SingleAsync(x => x.Id == requestId);
        Assert.Equal(status, request.Status);
        Assert.Equal(count, await db.Experts.Where(x => x.Id == expertId).Select(x => x.ActiveRequestCount).SingleAsync());
        await DatabaseInvariants.AssertAsync(db);
    }

    private sealed class TestUser : ICurrentUserContext { public int? UserId => null; }
    private sealed class InjectedException : Exception;
    private sealed class FailingRequestUpdate(IServiceRequestRepository inner, bool fail) : IServiceRequestRepository
    {
        public Task<int> TryMarkAssignedAsync(int requestId, int expertId, DateTime assignedAtUtc, RequestSubject subject, CancellationToken cancellationToken = default) =>
            fail ? Task.FromException<int>(new InjectedException()) : Task.FromResult(0);
        public Task AddAsync(ServiceRequest request, CancellationToken cancellationToken = default) => inner.AddAsync(request, cancellationToken);
        public void DiscardAdded(ServiceRequest request) => inner.DiscardAdded(request);
        public Task<ServiceRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => inner.GetByIdAsync(id, cancellationToken);
        public Task<ServiceRequest?> GetForAssignmentAsync(int id, CancellationToken cancellationToken = default) => inner.GetForAssignmentAsync(id, cancellationToken);
        public Task<ServiceRequest?> GetByRequestKeyAsync(Guid key, CancellationToken cancellationToken = default) => inner.GetByRequestKeyAsync(key, cancellationToken);
        public Task<int> TryMarkClosedAsync(int requestId, int expertId, DateTime closedAtUtc, CancellationToken cancellationToken = default) => inner.TryMarkClosedAsync(requestId, expertId, closedAtUtc, cancellationToken);
        public Task<PendingRequestPage> GetPendingPageAsync(DateTime scan, DateTime cutoff, int size, PendingQueueCursor? cursor, CancellationToken cancellationToken = default) => inner.GetPendingPageAsync(scan, cutoff, size, cursor, cancellationToken);
    }
}

public sealed class AssignmentFaultSqlFactAttribute : FactAttribute
{
    public AssignmentFaultSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ORGANIZATIONREQUESTS_ASSIGNMENT_FAULT_SQL")))
            Skip = "Set ORGANIZATIONREQUESTS_ASSIGNMENT_FAULT_SQL to a dedicated empty migrated SQL Server test database.";
    }
}
