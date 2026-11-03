using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Application.Services;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;
using Xunit;

namespace OrganizationRequests.Tests.Unit;

// بررسی ثبت و خواندن رویدادهای تاریخچه درخواست.
[Trait("Category", "Unit")]
public sealed class RequestHistoryTests
{
    [Fact]
    public async Task Records_distinct_actor_and_expert_and_complete_lifecycle()
    {
        var repository = new TestHistoryRepository();
        var history = new RequestHistoryService(repository);
        var now = DateTime.UtcNow;
        var request = new ServiceRequest { Id = 1, CreatedOn = now };
        await history.RecordRegistrationAsync(request, 8);
        await history.RecordAssignmentAsync(1, 5, now.AddSeconds(1));
        await history.RecordClosureAsync(1, 5, 42, now.AddSeconds(2));
        Assert.Collection(repository.Items,
            x => { Assert.Equal(RequestActivityType.Registered, x.EventType); Assert.Equal(8, x.ActorUserId); Assert.Null(x.ExpertId); },
            x => { Assert.Equal(RequestActivityType.Assigned, x.EventType); Assert.Equal(HistoryActorType.System, x.ActorType); Assert.Null(x.ActorUserId); Assert.Equal(5, x.ExpertId); },
            x => { Assert.Equal(RequestActivityType.Closed, x.EventType); Assert.Equal(42, x.ActorUserId); Assert.Equal(5, x.ExpertId); });
        Assert.Equal(now.AddSeconds(2), repository.Items[2].OccurredAtUtc);
    }

    [Fact]
    public async Task Request_events_use_chronological_order_with_id_tie_breaker()
    {
        var repository = new TestHistoryRepository();
        var time = DateTime.UtcNow;
        repository.Items.AddRange(new[]
        {
            new RequestActivityHistory { Id = 3, ServiceRequestId = 1, OccurredAtUtc = time.AddSeconds(1), EventType = RequestActivityType.Closed },
            new RequestActivityHistory { Id = 2, ServiceRequestId = 1, OccurredAtUtc = time, EventType = RequestActivityType.Assigned },
            new RequestActivityHistory { Id = 1, ServiceRequestId = 1, OccurredAtUtc = time, EventType = RequestActivityType.Registered },
            new RequestActivityHistory { Id = 4, ServiceRequestId = 2, OccurredAtUtc = time, EventType = RequestActivityType.Registered }
        });
        var events = await new RequestHistoryService(repository).GetRequestHistoryAsync(1);
        Assert.Equal(new[] { RequestActivityType.Registered, RequestActivityType.Assigned, RequestActivityType.Closed }, events.Select(x => x.EventType));
    }
    [Fact]
    public async Task Expert_pages_are_bounded_and_cursor_preserves_tie_order()
    {
        var repository = new TestHistoryRepository();
        var time = DateTime.UtcNow;
        for (var id = 1; id <= 5; id++) repository.Items.Add(new RequestActivityHistory
        { Id = id, ServiceRequestId = id, ExpertId = id == 5 ? 9 : 5, OccurredAtUtc = time,
            EventType = id % 2 == 0 ? RequestActivityType.Closed : RequestActivityType.Assigned,
            ServiceRequest = new ServiceRequest { Subject = RequestSubject.Technical } });
        var service = new RequestHistoryService(repository);
        var first = await service.GetExpertHistoryAsync(5, null, 2);
        var second = await service.GetExpertHistoryAsync(5, first.NextCursor, 2);
        Assert.Equal(new[] { 4, 3 }, first.Items.Select(x => x.Id));
        Assert.Equal(new[] { 2, 1 }, second.Items.Select(x => x.Id));
        Assert.Null(second.NextCursor);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetExpertHistoryAsync(5, null, 101));
    }
}
