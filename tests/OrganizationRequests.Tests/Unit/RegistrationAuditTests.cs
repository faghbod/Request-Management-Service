using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OrganizationRequests.Application.Abstractions;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;
using OrganizationRequests.Infrastructure.Persistence;
using OrganizationRequests.Infrastructure.Persistence.Repositories;
using Xunit;

namespace OrganizationRequests.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class RegistrationAuditTests
{
    [Fact]
    public async Task Pending_registration_history_gets_the_exact_request_audit_timestamp_before_save()
    {
        var interceptor = new StopBeforeDatabase();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=Unused;Integrated Security=True;TrustServerCertificate=True")
            .AddInterceptors(interceptor).Options;
        await using var db = new ApplicationDbContext(options, new User());
        var request = new ServiceRequest { RequestKey = Guid.NewGuid(), Subject = RequestSubject.Legal, Priority = RequestPriority.Normal };
        var history = new RequestActivityHistory { ServiceRequest = request, EventType = RequestActivityType.Registered,
            NewStatus = RequestStatus.PendingAssignment, ActorType = HistoryActorType.User, ActorUserId = 42 };
        db.RequestActivityHistories.Add(history);
        await Assert.ThrowsAsync<StoppedException>(() => db.SaveChangesAsync());
        Assert.True(interceptor.Called);
        Assert.Equal(request.CreatedOn, history.OccurredAtUtc);
        Assert.Equal(DateTimeKind.Utc, history.OccurredAtUtc.Kind);
        Assert.Equal(42, request.CreatedBy);
        Assert.Equal(EntityState.Added, db.Entry(request).State);
        Assert.Equal(EntityState.Added, db.Entry(history).State);
        new ServiceRequestRepository(db, new User()).DiscardAdded(request);
        Assert.Equal(EntityState.Detached, db.Entry(request).State);
        Assert.Equal(EntityState.Detached, db.Entry(history).State);
    }

    [Fact]
    public void Rollback_detaches_only_the_matching_unsaved_history()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=Unused;Integrated Security=True;TrustServerCertificate=True").Options;
        using var db = new ApplicationDbContext(options, new User());
        var pending = new RequestActivityHistory { ServiceRequestId = 12, EventType = RequestActivityType.Closed };
        var other = new RequestActivityHistory { ServiceRequestId = 13, EventType = RequestActivityType.Closed };
        db.RequestActivityHistories.AddRange(pending, other);
        new RequestActivityHistoryRepository(db).DiscardPending(12, RequestActivityType.Closed);
        Assert.Equal(EntityState.Detached, db.Entry(pending).State);
        Assert.Equal(EntityState.Added, db.Entry(other).State);
    }
    private sealed class User : ICurrentUserContext { public int? UserId => 42; public int? ExpertId => null; }
    private sealed class StoppedException : Exception;
    private sealed class StopBeforeDatabase : SaveChangesInterceptor
    {
        public bool Called { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Called = true;
            throw new StoppedException();
        }
    }
}
