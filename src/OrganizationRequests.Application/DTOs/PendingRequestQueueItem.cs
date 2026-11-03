using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Application.DTOs;

// اطلاعات موردنیاز Worker برای یک درخواست در صف.
public sealed record PendingRequestQueueItem(
    int Id,
    RequestSubject Subject,
    RequestPriority Priority,
    DateTime CreatedOn,
    int EffectivePriorityRank);
