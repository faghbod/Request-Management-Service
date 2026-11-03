using System.Data.Common;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
using OrganizationRequests.Infrastructure.Persistence.Repositories;
using OrganizationRequests.Worker.Options;
using OrganizationRequests.Worker.Workers;
using Xunit;
using Xunit.Abstractions;

namespace OrganizationRequests.Tests.Performance;

// بررسی عملکرد خواندن صف با داده‌های SQL Server.
[Trait("Category", "Performance")]
[Trait("Category", "SqlServer")]
public sealed class QueuePerformanceSqlTests(ITestOutputHelper output)
{
    [PerformanceSqlFact]
    public async Task Measure_queue_reads_at_100_and_10000_pending_requests()
    {
        var connection = Environment.GetEnvironmentVariable("ORGANIZATIONREQUESTS_PERF_TEST_SQL")!;
        Assert.StartsWith("OrganizationRequests_Test_", new SqlConnectionStringBuilder(connection).InitialCatalog, StringComparison.Ordinal);
        await using var provider = Provider(connection);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Contains("20261102090017_InitialOrganizationRequests", await db.Database.GetAppliedMigrationsAsync());
            Assert.False(await db.Experts.AnyAsync());
            Assert.False(await db.ServiceRequests.AnyAsync());
        }
        await Seed(provider, 100);
        await MeasurePages(connection, 100);

        // This deliberately small cycle measures orchestration with no eligible experts.
        var worker = new AssignmentWorker(provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new AssignmentWorkerOptions()), NullLogger<AssignmentWorker>.Instance);
        var cycle = Stopwatch.StartNew();
        var stats = await worker.ProcessQueueCycleAsync(CancellationToken.None);
        cycle.Stop();
        worker.Dispose();
        Assert.Equal(100, stats.Inspected);
        output.WriteLine("100-request cycle: {0} ms, {1} pages, {2} unassignable, {3} retries", cycle.ElapsedMilliseconds, stats.Pages, stats.Unavailable, stats.Retries);

        await Seed(provider, 9900);
        await MeasurePages(connection, 10000);
        output.WriteLine("No SQL execution plan, assignment throughput, or concurrent-worker measurement was collected by this test.");
    }

    private async Task MeasurePages(string connection, int expected)
    {
        var counter = new QueryCounter();
        var stopwatch = Stopwatch.StartNew();
        var pages = 0;
        var inspected = 0;
        var scan = DateTime.UtcNow;
        PendingQueueCursor? cursor = null;
        while (true)
        {
            var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).AddInterceptors(counter).Options;
            await using var db = new ApplicationDbContext(dbOptions, new TestUser());
            var service = new PendingRequestQueueService(new ServiceRequestRepository(db, new TestUser()),
                Options.Create(new QueueProcessingOptions { BatchSize = 50, AgingThresholdMinutes = 30 }));
            var page = await service.GetNextPageAsync(scan, cursor);
            pages++;
            inspected += page.Items.Count;
            if (!page.HasMore) break;
            cursor = page.NextCursor ?? throw new InvalidOperationException("Missing cursor");
        }
        stopwatch.Stop();
        Assert.Equal(expected, inspected);
        output.WriteLine("{0} pending: {1} ms, {2} pages, {3} SQL reader commands", expected, stopwatch.ElapsedMilliseconds, pages, counter.Commands);
    }

    private static async Task Seed(ServiceProvider provider, int count)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ServiceRequests.AddRange(Enumerable.Range(0, count).Select(i => new ServiceRequest
        {
            RequestKey = Guid.NewGuid(), Status = RequestStatus.PendingAssignment,
            Subject = (RequestSubject)(i % 3 + 1), Priority = i % 3 == 0 ? RequestPriority.Urgent : RequestPriority.Normal
        }));
        await db.SaveChangesAsync();
        await db.ServiceRequests.Where(x => x.Id % 4 == 0).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedOn, DateTime.UtcNow.AddMinutes(-40)));
    }

    private static ServiceProvider Provider(string connection)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:DefaultConnection"] = connection }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ICurrentUserContext, TestUser>();
        services.AddApplication();
        services.AddInfrastructure(config);
        services.Configure<QueueProcessingOptions>(o => o.BatchSize = 50);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
    private sealed class TestUser : ICurrentUserContext { public int? UserId => null; public int? ExpertId => null; }
    private sealed class QueryCounter : DbCommandInterceptor
    {
        public int Commands;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Commands);
            return ValueTask.FromResult(result);
        }
    }
}

public sealed class PerformanceSqlFactAttribute : FactAttribute
{
    public PerformanceSqlFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("ORGANIZATIONREQUESTS_RUN_PERFORMANCE") != "1" ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ORGANIZATIONREQUESTS_PERF_TEST_SQL")))
            Skip = "Opt in with ORGANIZATIONREQUESTS_RUN_PERFORMANCE=1 and a dedicated empty ORGANIZATIONREQUESTS_PERF_TEST_SQL database.";
    }
}
