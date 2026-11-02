using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Domain.IRepositories;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Application.Services;

// The calling workflow owns authorization, SaveChanges and transaction boundaries.
public sealed class RequestHistoryService(IRequestActivityHistoryRepository repository) : IRequestHistoryService
{
    public void DiscardPending(int requestId, RequestActivityType eventType) => repository.DiscardPending(requestId, eventType);

    public Task RecordRegistrationAsync(ServiceRequest request, int userId, CancellationToken cancellationToken = default) =>
        repository.AddAsync(new RequestActivityHistory { ServiceRequest = request, EventType = RequestActivityType.Registered,
            NewStatus = RequestStatus.PendingAssignment, ActorType = HistoryActorType.User, ActorUserId = userId,
            OccurredAtUtc = request.CreatedOn, Description = "Request registered" }, cancellationToken);

    public Task RecordAssignmentAsync(int requestId, int expertId, DateTime occurredAtUtc, CancellationToken cancellationToken = default) =>
        repository.AddAsync(new RequestActivityHistory { ServiceRequestId = requestId, ExpertId = expertId,
            EventType = RequestActivityType.Assigned, PreviousStatus = RequestStatus.PendingAssignment, NewStatus = RequestStatus.Assigned,
            ActorType = HistoryActorType.System, OccurredAtUtc = occurredAtUtc, Description = "Request assigned to expert" }, cancellationToken);

    public Task RecordClosureAsync(int requestId, int expertId, int userId, DateTime occurredAtUtc, CancellationToken cancellationToken = default) =>
        repository.AddAsync(new RequestActivityHistory { ServiceRequestId = requestId, ExpertId = expertId,
            EventType = RequestActivityType.Closed, PreviousStatus = RequestStatus.Assigned, NewStatus = RequestStatus.Closed,
            ActorType = HistoryActorType.User, ActorUserId = userId, OccurredAtUtc = occurredAtUtc, Description = "Request closed" }, cancellationToken);

    public async Task<IReadOnlyList<RequestHistoryItemDto>> GetRequestHistoryAsync(int requestId, CancellationToken cancellationToken = default) =>
        (await repository.GetByRequestIdAsync(requestId, cancellationToken)).Select(x => new RequestHistoryItemDto(
            x.EventType, x.PreviousStatus, x.NewStatus, x.ExpertId, x.ActorType, DateTime.SpecifyKind(x.OccurredAtUtc, DateTimeKind.Utc))).ToArray();

    public async Task<ExpertHistoryPage> GetExpertHistoryAsync(int expertId, ExpertHistoryCursor? cursor, int pageSize, CancellationToken cancellationToken = default)
    {
        if (expertId <= 0) throw new ArgumentOutOfRangeException(nameof(expertId));
        if (pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));
        if (cursor is not null && (cursor.Id <= 0 || cursor.OccurredAtUtc == default)) throw new ArgumentException("Invalid cursor.", nameof(cursor));
        var (items, hasMore) = await repository.GetByExpertIdAsync(expertId, cursor?.OccurredAtUtc, cursor?.Id, pageSize, cancellationToken);
        var result = items.Select(x => new ExpertHistoryItemDto(x.Id, x.ServiceRequestId, x.ServiceRequest.Subject,
            x.EventType, x.PreviousStatus, x.NewStatus, x.ActorType, x.ActorUserId, DateTime.SpecifyKind(x.OccurredAtUtc, DateTimeKind.Utc), x.Description)).ToArray();
        var last = items.LastOrDefault();
        return new ExpertHistoryPage(result, hasMore && last is not null ? new ExpertHistoryCursor(DateTime.SpecifyKind(last.OccurredAtUtc, DateTimeKind.Utc), last.Id) : null);
    }
}
