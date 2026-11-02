using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Application.DTOs;

public sealed record RequestHistoryItemDto(RequestActivityType EventType, RequestStatus? PreviousStatus,
    RequestStatus NewStatus, int? ExpertId, HistoryActorType ActorType, DateTime OccurredAtUtc);

public sealed record ExpertHistoryItemDto(int Id, int ServiceRequestId, RequestSubject RequestSubject,
    RequestActivityType EventType, RequestStatus? PreviousStatus, RequestStatus NewStatus,
    HistoryActorType ActorType, int? ActorUserId, DateTime OccurredAtUtc, string? Description);
