using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrganizationRequests.Application;
using OrganizationRequests.Application.Abstractions;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Application.Options;
using OrganizationRequests.Application.Services;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;
using OrganizationRequests.Infrastructure;
using OrganizationRequests.Infrastructure.Persistence;
using OrganizationRequests.Worker.Options;
using OrganizationRequests.Worker.Workers;
using OrganizationRequests.Tests.Integration;
using Xunit;

namespace OrganizationRequests.Tests;

// Requires a dedicated, empty database with migrations already applied. Never creates or drops a database.
[Trait("Category", "SqlServer")]
[Trait("Category", "Concurrency")]
public sealed class SqlServerConcurrencyTests
{
    [SqlServerFact]
    public async Task Concurrent_attempts_and_independent_workers_preserve_capacity_and_consistency()
    {
        var connection = Environment.GetEnvironmentVariable("ORGANIZATIONREQUESTS_TEST_SQL")!;
        var database = new SqlConnectionStringBuilder(connection).InitialCatalog;
        Assert.StartsWith("OrganizationRequests_Test_", database, StringComparison.Ordinal);
        await using var first = CreateProvider(connection);
        await using var second = CreateProvider(connection);
        int[] ids;
        int expertId;
        await using (var scope = first.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Contains("20261102090017_InitialOrganizationRequests", await db.Database.GetAppliedMigrationsAsync());
            Assert.False(await db.Experts.AnyAsync());
            Assert.False(await db.ServiceRequests.AnyAsync());
            var expert = new Expert { Name = "Integration test expert", IsActive = true, Capacity = 5,
                Specialties = [new ExpertSpecialty { Subject = RequestSubject.Technical }] };
            var requests = Enumerable.Range(0, 20).Select(_ => new ServiceRequest
            {
                RequestKey = Guid.NewGuid(), Subject = RequestSubject.Technical,
                Priority = RequestPriority.Normal, Status = RequestStatus.PendingAssignment
            }).ToArray();
            db.Experts.Add(expert);
            db.ServiceRequests.AddRange(requests);
            await db.SaveChangesAsync();
            ids = requests.Select(x => x.Id).ToArray();
            expertId = expert.Id;
        }

        // The same request can be seen by both instances at once.
        var sameRequest = await Task.WhenAll(Attempt(first, ids[0]), Attempt(second, ids[0]));
        Assert.Equal(1, sameRequest.Count(x => x == AssignmentResultStatus.Assigned));

        // Concurrent attempts for all twenty requests use a fresh scope for each attempt.
        await Task.WhenAll(ids.Select((id, index) => Attempt(index % 2 == 0 ? first : second, id)));

        // Two separate containers concurrently scan the same queue; subsequent cycles drain conflicts.
        var worker1 = MakeWorker(first);
        var worker2 = MakeWorker(second);
        await Task.WhenAll(worker1.ProcessQueueCycleAsync(CancellationToken.None), worker2.ProcessQueueCycleAsync(CancellationToken.None));
        await worker1.ProcessQueueCycleAsync(CancellationToken.None);
        worker1.Dispose();
        worker2.Dispose();

        await using var checkScope = first.CreateAsyncScope();
        var check = checkScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var assigned = await check.ServiceRequests.CountAsync(x => x.Status == RequestStatus.Assigned && x.AssignedExpertId == expertId);
        var pending = await check.ServiceRequests.CountAsync(x => x.Status == RequestStatus.PendingAssignment);
        var count = await check.Experts.Where(x => x.Id == expertId).Select(x => x.ActiveRequestCount).SingleAsync();
        Assert.Equal(5, assigned);
        Assert.Equal(15, pending);
        Assert.Equal(assigned, count);
        var events = await check.RequestActivityHistories.AsNoTracking().Where(x => x.EventType == RequestActivityType.Assigned).ToListAsync();
        Assert.Equal(assigned, events.Count);
        Assert.Equal(events.Count, events.Select(x => x.ServiceRequestId).Distinct().Count());
        Assert.All(events, x => { Assert.Equal(expertId, x.ExpertId); Assert.Null(x.ActorUserId); });
        await DatabaseInvariants.AssertAsync(check);
    }

    private static async Task<AssignmentResultStatus> Attempt(ServiceProvider provider, int id)
    {
        await using var scope = provider.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<IRequestAssignmentService>().TryAssignAsync(id)).Status;
    }

    private static ServiceProvider CreateProvider(string connection)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:DefaultConnection"] = connection }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ICurrentUserContext, TestSystemContext>();
        services.AddApplication();
        services.AddInfrastructure(config);
        services.Configure<QueueProcessingOptions>(o => o.BatchSize = 5);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static AssignmentWorker MakeWorker(ServiceProvider provider) => new(
        provider.GetRequiredService<IServiceScopeFactory>(),
        Options.Create(new AssignmentWorkerOptions { MaxAssignmentAttempts = 10, InitialRetryDelayMilliseconds = 10, MaxRetryDelayMilliseconds = 100 }),
        NullLogger<AssignmentWorker>.Instance);

    private sealed class TestSystemContext : ICurrentUserContext { public int? UserId => null; public int? ExpertId => null; }
}

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ORGANIZATIONREQUESTS_TEST_SQL")))
            Skip = "Set ORGANIZATIONREQUESTS_TEST_SQL to a dedicated empty migrated test database.";
    }
}
