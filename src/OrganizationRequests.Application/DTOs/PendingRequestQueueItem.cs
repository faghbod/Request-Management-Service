using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Application.DTOs;

public sealed record PendingRequestQueueItem(
    int Id,
    RequestSubject Subject,
    RequestPriority Priority,
    DateTime CreatedOn,
    int EffectivePriorityRank);
