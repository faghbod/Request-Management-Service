using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrganizationRequests.Application;
using OrganizationRequests.Application.Abstractions;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Application.Options;
using OrganizationRequests.Application.Repositories;
using OrganizationRequests.Application.Services;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;
using OrganizationRequests.Domain.UnitOfWork;
using OrganizationRequests.Infrastructure;
using OrganizationRequests.Infrastructure.Persistence;
using OrganizationRequests.Worker.Options;
using OrganizationRequests.Worker.Workers;
using OrganizationRequests.Tests.Integration;
using Xunit;

namespace OrganizationRequests.Tests;

[Trait("Category", "SqlServer")]
[Trait("Category", "Concurrency")]
public sealed class SqlServerClosureTests
{
    [ClosureSqlServerFact]
    public async Task Concurrent_closure_assignment_rollback_and_worker_capacity_remain_consistent()
    {
        var connection = Environment.GetEnvironmentVariable("ORGANIZATIONREQUESTS_CLOSURE_TEST_SQL")!;
        Assert.StartsWith("OrganizationRequests_Test_", new SqlConnectionStringBuilder(connection).InitialCatalog, StringComparison.Ordinal);
        await using var first = Provider(connection);
        await using var second = Provider(connection);
        int expertId, a, b, c, pending;
        await using (var scope = first.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Contains("20261102090017_InitialOrganizationRequests", await db.Database.GetAppliedMigrationsAsync());
            Assert.False(await db.Experts.AnyAsync());
            Assert.False(await db.ServiceRequests.AnyAsync());
            var expert = new Expert { Name = "Closure integration expert", Capacity = 3, ActiveRequestCount = 3, IsActive = true,
                Specialties = [new ExpertSpecialty { Subject = RequestSubject.Technical }] };
            var assigned = Enumerable.Range(0, 3).Select(_ => new ServiceRequest
            {
                RequestKey = Guid.NewGuid(), Subject = RequestSubject.Technical, Priority = RequestPriority.Normal,
                Status = RequestStatus.Assigned, AssignedExpert = expert, AssignedAt = DateTime.UtcNow.AddMinutes(-10)
            }).ToArray();
            var waiting = new ServiceRequest { RequestKey = Guid.NewGuid(), Subject = RequestSubject.Technical,
                Priority = RequestPriority.Normal, Status = RequestStatus.PendingAssignment };
            db.ServiceRequests.AddRange(assigned);
            db.ServiceRequests.Add(waiting);
            await db.SaveChangesAsync();
            expertId = expert.Id;
            (a, b, c, pending) = (assigned[0].Id, assigned[1].Id, assigned[2].Id, waiting.Id);
        }

        // Inject a failure after the request UPDATE and verify the real SQL transaction rolls back.
        await using (var scope = first.CreateAsyncScope())
        {
            var sp = scope.ServiceProvider;
            var service = new RequestClosureService(sp.GetRequiredService<IServiceRequestRepository>(), sp.GetRequiredService<IExpertRepository>(),
                new FailingSaveUnitOfWork(sp.GetRequiredService<IUnitOfWork>()), sp.GetRequiredService<IPersistenceErrorClassifier>(),
                NullLogger<RequestClosureService>.Instance);
            await Assert.ThrowsAsync<InjectedException>(() => service.CloseAsync(a));
        }
        await using (var scope = first.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var request = await db.ServiceRequests.AsNoTracking().SingleAsync(x => x.Id == a);
            Assert.Equal(RequestStatus.Assigned, request.Status);
            Assert.Null(request.ClosedAt);
            Assert.Equal(3, await db.Experts.Where(x => x.Id == expertId).Select(x => x.ActiveRequestCount).SingleAsync());
        }

        var duplicate = await Task.WhenAll(Close(first, a), Close(second, a));
        Assert.Equal(1, duplicate.Count(x => x.Status == RequestClosureResultStatus.Closed));
        Assert.Contains(duplicate, x => x.Status is RequestClosureResultStatus.AlreadyClosed or RequestClosureResultStatus.RetryableConflict);
        var persistedClose = duplicate.Single(x => x.Status == RequestClosureResultStatus.Closed).ClosedAt;
        Assert.Equal(persistedClose, (await Close(first, a)).ClosedAt);

        // Close two different requests while another scope tries assignment against the same expert.
        var closeB = Close(first, b);
        var closeC = Close(second, c);
        var assign = Assign(first, pending);
        await Task.WhenAll(closeB, closeC, assign);
        var operations = new[] { await closeB, await closeC };
        foreach (var (id, result) in new[] { (b, operations[0]), (c, operations[1]) })
        {
            if (result.Status == RequestClosureResultStatus.RetryableConflict)
                Assert.Equal(RequestClosureResultStatus.Closed, (await Close(first, id)).Status);
            else
                Assert.Equal(RequestClosureResultStatus.Closed, result.Status);
        }

        var worker = new AssignmentWorker(first.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new AssignmentWorkerOptions { InitialRetryDelayMilliseconds = 0, MaxRetryDelayMilliseconds = 0 }),
            NullLogger<AssignmentWorker>.Instance);
        await worker.ProcessQueueCycleAsync(CancellationToken.None);
        worker.Dispose();

        await using var verify = first.CreateAsyncScope();
        var context = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var requests = await context.ServiceRequests.AsNoTracking().ToListAsync();
        Assert.All(requests.Where(x => x.Id == a || x.Id == b || x.Id == c), x =>
        {
            Assert.Equal(RequestStatus.Closed, x.Status);
            Assert.Equal(expertId, x.AssignedExpertId);
            Assert.NotNull(x.AssignedAt);
            Assert.NotNull(x.ClosedAt);
        });
        Assert.Equal(RequestStatus.Assigned, requests.Single(x => x.Id == pending).Status);
        var count = await context.Experts.Where(x => x.Id == expertId).Select(x => x.ActiveRequestCount).SingleAsync();
        Assert.Equal(1, count);
        Assert.Equal(count, requests.Count(x => x.AssignedExpertId == expertId && x.Status == RequestStatus.Assigned));

        // Verify that SQL Server rowversion rejects a stale tracked expert update.
        await using var staleScope = first.CreateAsyncScope();
        await using var currentScope = second.CreateAsyncScope();
        var staleDb = staleScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var currentDb = currentScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var staleExpert = await staleDb.Experts.SingleAsync(x => x.Id == expertId);
        var currentExpert = await currentDb.Experts.SingleAsync(x => x.Id == expertId);
        currentExpert.Name = "Current SQL Server version";
        await currentDb.SaveChangesAsync();
        staleExpert.Name = "Stale SQL Server version";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => staleDb.SaveChangesAsync());

        // An independently seeded full expert proves a later worker cycle uses released capacity.
        int priorId, waitingId, legalExpertId;
        await using (var seed = first.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var legal = new Expert { Name = "Capacity release expert", Capacity = 1, ActiveRequestCount = 1, IsActive = true,
                Specialties = [new ExpertSpecialty { Subject = RequestSubject.Legal }] };
            var prior = new ServiceRequest { RequestKey = Guid.NewGuid(), Subject = RequestSubject.Legal,
                Priority = RequestPriority.Normal, Status = RequestStatus.Assigned, AssignedExpert = legal, AssignedAt = DateTime.UtcNow.AddMinutes(-5) };
            var waiting = new ServiceRequest { RequestKey = Guid.NewGuid(), Subject = RequestSubject.Legal,
                Priority = RequestPriority.Normal, Status = RequestStatus.PendingAssignment };
            db.ServiceRequests.AddRange(prior, waiting);
            await db.SaveChangesAsync();
            (priorId, waitingId, legalExpertId) = (prior.Id, waiting.Id, legal.Id);
        }
        Assert.Equal(RequestClosureResultStatus.Closed, (await Close(first, priorId)).Status);
        var nextWorker = new AssignmentWorker(first.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new AssignmentWorkerOptions { InitialRetryDelayMilliseconds = 0, MaxRetryDelayMilliseconds = 0 }),
            NullLogger<AssignmentWorker>.Instance);
        await nextWorker.ProcessQueueCycleAsync(CancellationToken.None);
        nextWorker.Dispose();
        await using var capacityScope = first.CreateAsyncScope();
        var capacityDb = capacityScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var priorState = await capacityDb.ServiceRequests.AsNoTracking().SingleAsync(x => x.Id == priorId);
        var waitingState = await capacityDb.ServiceRequests.AsNoTracking().SingleAsync(x => x.Id == waitingId);
        Assert.Equal(RequestStatus.Closed, priorState.Status);
        Assert.Equal(RequestStatus.Assigned, waitingState.Status);
        Assert.Equal(legalExpertId, waitingState.AssignedExpertId);
        Assert.Equal(1, await capacityDb.Experts.Where(x => x.Id == legalExpertId).Select(x => x.ActiveRequestCount).SingleAsync());
        await DatabaseInvariants.AssertAsync(capacityDb);
    }

    private static async Task<RequestClosureResult> Close(ServiceProvider provider, int id)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRequestClosureService>().CloseAsync(id);
    }

    private static async Task<AssignmentResult> Assign(ServiceProvider provider, int id)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRequestAssignmentService>().TryAssignAsync(id);
    }

    private static ServiceProvider Provider(string connection)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:DefaultConnection"] = connection }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ICurrentUserContext, SystemContext>();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.Configure<QueueProcessingOptions>(_ => { });
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private sealed class SystemContext : ICurrentUserContext { public int? UserId => null; }
    private sealed class InjectedException : Exception;
    private sealed class FailingSaveUnitOfWork(IUnitOfWork inner) : IUnitOfWork
    {
        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => inner.BeginTransactionAsync(cancellationToken);
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromException<int>(new InjectedException());
    }
}

public sealed class ClosureSqlServerFactAttribute : FactAttribute
{
    public ClosureSqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ORGANIZATIONREQUESTS_CLOSURE_TEST_SQL")))
            Skip = "Set ORGANIZATIONREQUESTS_CLOSURE_TEST_SQL to a dedicated empty migrated test database.";
    }
}
