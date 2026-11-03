using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OrganizationRequests.Application;
using OrganizationRequests.Application.Abstractions;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Application.Exceptions;
using OrganizationRequests.Application.Options;
using OrganizationRequests.Domain.IRepositories;
using OrganizationRequests.Application.Services;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;
using OrganizationRequests.Domain.UnitOfWork;
using OrganizationRequests.Infrastructure;
using OrganizationRequests.Infrastructure.Persistence;
using Xunit;

namespace OrganizationRequests.Tests.Integration;

// بررسی ثبت هم‌زمان، تکرار و صفحه‌بندی صف با SQL Server.
[Trait("Category", "SqlServer")]
[Trait("Category", "Concurrency")]
public sealed class RegistrationQueueSqlTests
{
    [Phase10SqlFact]
    public async Task Registration_races_selection_replay_and_stable_queue_pages()
    {
        var connection = Environment.GetEnvironmentVariable("ORGANIZATIONREQUESTS_PHASE10_SQL")!;
        Assert.StartsWith("OrganizationRequests_Test_", new SqlConnectionStringBuilder(connection).InitialCatalog, StringComparison.Ordinal);
        await using var provider = CreateProvider(connection);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Contains("20261102090017_InitialOrganizationRequests", await db.Database.GetAppliedMigrationsAsync());
            Assert.False(await db.Experts.AnyAsync());
            Assert.False(await db.ServiceRequests.AnyAsync());
        }

        var sameKey = Guid.NewGuid();
        var input = new RegisterRequestDto { RequestKey = sameKey, Subject = RequestSubject.Technical, Priority = RequestPriority.Normal };
        var gate = new InitialReadGate(20);
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Register(provider, input, gate)));
        Assert.Single(results, x => x.IsNewlyCreated);
        Assert.Single(results.Select(x => x.Request.Id).Distinct());
        var requestId = results[0].Request.Id;
        var replay = await Register(provider, input);
        Assert.False(replay.IsNewlyCreated);
        Assert.Equal(requestId, replay.Request.Id);
        await Assert.ThrowsAsync<RequestKeyConflictException>(() => Register(provider, new RegisterRequestDto
        { RequestKey = sameKey, Subject = RequestSubject.Legal, Priority = RequestPriority.Normal }));

        var unique = Enumerable.Range(0, 20).Select(_ => new RegisterRequestDto
        { RequestKey = Guid.NewGuid(), Subject = RequestSubject.Financial, Priority = RequestPriority.Normal }).ToArray();
        var uniqueResults = await Task.WhenAll(unique.Select(item => Register(provider, item)));
        Assert.Equal(20, uniqueResults.Select(x => x.Request.Id).Distinct().Count());
        Assert.All(uniqueResults, x => Assert.Equal(RequestStatus.PendingAssignment, x.Request.Status));

        int expertId, legalExpertId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(1, await db.ServiceRequests.CountAsync(x => x.RequestKey == sameKey));
            Assert.Equal(21, await db.ServiceRequests.CountAsync());
            Assert.All(await db.ServiceRequests.AsNoTracking().ToListAsync(), x =>
            { Assert.Equal(RequestStatus.PendingAssignment, x.Status); Assert.Null(x.AssignedExpertId); });
            var inactive = new Expert { Name = "Inactive", Capacity = 1, IsActive = false,
                Specialties = [new ExpertSpecialty { Subject = RequestSubject.Technical }, new ExpertSpecialty { Subject = RequestSubject.Financial }] };
            var deleted = new Expert { Name = "Deleted", Capacity = 1, IsActive = true,
                Specialties = [new ExpertSpecialty { Subject = RequestSubject.Technical }, new ExpertSpecialty { Subject = RequestSubject.Financial }] };
            var legal = new Expert { Name = "Legal only", Capacity = 1, IsActive = true,
                Specialties = [new ExpertSpecialty { Subject = RequestSubject.Legal }] };
            var full = new Expert { Name = "Full", Capacity = 1, ActiveRequestCount = 1, IsActive = true,
                Specialties = [new ExpertSpecialty { Subject = RequestSubject.Technical }, new ExpertSpecialty { Subject = RequestSubject.Financial }] };
            var expert = new Expert { Name = "Multi-specialty", Capacity = 1, IsActive = true,
                Specialties = [new ExpertSpecialty { Subject = RequestSubject.Technical }, new ExpertSpecialty { Subject = RequestSubject.Financial }] };
            db.Experts.AddRange(inactive, deleted, legal, full, expert);
            db.ServiceRequests.Add(new ServiceRequest { RequestKey = Guid.NewGuid(), Subject = RequestSubject.Technical,
                Priority = RequestPriority.Normal, Status = RequestStatus.Assigned, AssignedExpert = full, AssignedAt = DateTime.UtcNow.AddMinutes(-5) });
            await db.SaveChangesAsync();
            expertId = expert.Id;
            provider.GetRequiredService<TestUser>().ExpertId = expertId;
            legalExpertId = legal.Id;
            await db.Experts.Where(x => x.Id == deleted.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDeleted, true));
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var selection = scope.ServiceProvider.GetRequiredService<IExpertSelectionService>();
            Assert.Equal(expertId, (await selection.SelectBestExpertAsync(RequestSubject.Technical))!.Id);
            Assert.Equal(expertId, (await selection.SelectBestExpertAsync(RequestSubject.Financial))!.Id);
            Assert.Equal(legalExpertId, (await selection.SelectBestExpertAsync(RequestSubject.Legal))!.Id);
            Assert.Equal(AssignmentResultStatus.Assigned,
                (await scope.ServiceProvider.GetRequiredService<IRequestAssignmentService>().TryAssignAsync(requestId)).Status);
        }
        // A new scope observes that capacity is shared between specialties.
        await using (var scope = provider.CreateAsyncScope())
            Assert.Null(await scope.ServiceProvider.GetRequiredService<IExpertSelectionService>().SelectBestExpertAsync(RequestSubject.Financial));
        var afterAssignment = await Register(provider, input);
        Assert.Equal(RequestStatus.Assigned, afterAssignment.Request.Status);
        await using (var scope = provider.CreateAsyncScope())
            Assert.Equal(RequestClosureResultStatus.Closed, (await scope.ServiceProvider.GetRequiredService<IRequestClosureService>().CloseAsync(requestId)).Status);
        var afterClosure = await Register(provider, input);
        Assert.Equal(RequestStatus.Closed, afterClosure.Request.Status);
        Assert.Equal(requestId, afterClosure.Request.Id);

        await VerifyQueuePages(provider);
        await using var checkScope = provider.CreateAsyncScope();
        await DatabaseInvariants.AssertAsync(checkScope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private static async Task VerifyQueuePages(ServiceProvider provider)
    {
        var scan = DateTime.UtcNow.AddDays(-1);
        var items = Enumerable.Range(0, 120).Select(i => new ServiceRequest
        {
            RequestKey = Guid.NewGuid(), Subject = RequestSubject.Legal,
            Priority = i == 2 ? RequestPriority.Urgent : RequestPriority.Normal, Status = RequestStatus.PendingAssignment
        }).ToArray();
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ServiceRequests.AddRange(items);
            await db.SaveChangesAsync();
            var normalIds = items.Skip(3).Select(x => x.Id).ToArray();
            await db.ServiceRequests.Where(x => normalIds.Contains(x.Id)).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedOn, scan.AddMinutes(-1)));
            await db.ServiceRequests.Where(x => x.Id == items[0].Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedOn, scan.AddMinutes(-30)));
            await db.ServiceRequests.Where(x => x.Id == items[1].Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedOn, scan.AddMinutes(-40)));
            await db.ServiceRequests.Where(x => x.Id == items[2].Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedOn, scan.AddMinutes(-5)));
        }
        var expected = new[] { items[1].Id, items[0].Id, items[2].Id }.Concat(items.Skip(3).Select(x => x.Id).Order()).ToArray();
        var actual = new List<int>();
        var sizes = new List<int>();
        PendingQueueCursor? cursor = null;
        do
        {
            await using var scope = provider.CreateAsyncScope();
            var page = await scope.ServiceProvider.GetRequiredService<IPendingRequestQueueService>().GetNextPageAsync(scan, cursor);
            sizes.Add(page.Items.Count);
            actual.AddRange(page.Items.Select(x => x.Id));
            cursor = page.NextCursor;
            if (!page.HasMore) break;
            Assert.NotNull(cursor);
        } while (true);
        Assert.Equal(new[] { 50, 50, 20 }, sizes);
        Assert.Equal(expected, actual);
        Assert.Equal(120, actual.Distinct().Count());
        // Registrations created after scanStartedAtUtc were excluded from this scan.
    }

    private static async Task<RegisterRequestResult> Register(ServiceProvider provider, RegisterRequestDto input, InitialReadGate? gate = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var repository = sp.GetRequiredService<IServiceRequestRepository>();
        if (gate is not null) repository = new GatedRepository(repository, gate);
        var service = new RequestRegistrationService(repository, sp.GetRequiredService<IUnitOfWork>(),
            sp.GetRequiredService<IPersistenceErrorClassifier>(), sp.GetRequiredService<ICurrentUserContext>(), sp.GetRequiredService<IRequestHistoryService>(), NullLogger<RequestRegistrationService>.Instance);
        return await service.RegisterAsync(input);
    }

    private static ServiceProvider CreateProvider(string connection)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:DefaultConnection"] = connection }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TestUser>();
        services.AddScoped<ICurrentUserContext>(sp => sp.GetRequiredService<TestUser>());
        services.AddApplication();
        services.AddInfrastructure(config);
        services.Configure<QueueProcessingOptions>(o => { o.BatchSize = 50; o.AgingThresholdMinutes = 30; });
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
    private sealed class TestUser : ICurrentUserContext { public int? UserId => 101; public int? ExpertId { get; set; } }

    private sealed class InitialReadGate(int participants)
    {
        private int arrived;
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task WaitAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref arrived) == participants) ready.TrySetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(60), cancellationToken);
        }
    }

    private sealed class GatedRepository(IServiceRequestRepository inner, InitialReadGate gate) : IServiceRequestRepository
    {
        private int read;
        public async Task<ServiceRequest?> GetByRequestKeyAsync(Guid key, CancellationToken cancellationToken = default)
        {
            var found = await inner.GetByRequestKeyAsync(key, cancellationToken);
            if (Interlocked.Increment(ref read) == 1 && found is null) await gate.WaitAsync(cancellationToken);
            return found;
        }
        public Task AddAsync(ServiceRequest request, CancellationToken cancellationToken = default) => inner.AddAsync(request, cancellationToken);
        public void DiscardAdded(ServiceRequest request) => inner.DiscardAdded(request);
        public Task<ServiceRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => inner.GetByIdAsync(id, cancellationToken);
        public Task<ServiceRequest?> GetForAssignmentAsync(int id, CancellationToken cancellationToken = default) => inner.GetForAssignmentAsync(id, cancellationToken);
        public Task<int> TryMarkAssignedAsync(int requestId, int expertId, DateTime assignedAtUtc, RequestSubject subject, CancellationToken cancellationToken = default) => inner.TryMarkAssignedAsync(requestId, expertId, assignedAtUtc, subject, cancellationToken);
        public Task<int> TryMarkClosedAsync(int requestId, int expertId, DateTime closedAtUtc, CancellationToken cancellationToken = default) => inner.TryMarkClosedAsync(requestId, expertId, closedAtUtc, cancellationToken);

    }
}

public sealed class Phase10SqlFactAttribute : FactAttribute
{
    public Phase10SqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ORGANIZATIONREQUESTS_PHASE10_SQL")))
            Skip = "Set ORGANIZATIONREQUESTS_PHASE10_SQL to a dedicated empty migrated SQL Server test database.";
    }
}
