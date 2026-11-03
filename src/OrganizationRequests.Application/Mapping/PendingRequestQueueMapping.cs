using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Application.Mapping;

// تبدیل خروجی صف در خود کوئری؛ فقط ستون‌های موردنیاز DTO از دیتابیس خوانده می‌شوند.
public static class PendingRequestQueueMapping
{
    public static IQueryable<PendingRequestQueueItem> ToPendingQueueItems(
        this IQueryable<ServiceRequest> requests, DateTime agingCutoffUtc) =>
        requests.Select(request => new PendingRequestQueueItem(
            request.Id,
            request.Subject,
            request.Priority,
            request.CreatedOn,
            request.Priority == RequestPriority.Urgent ||
            (request.Priority == RequestPriority.Normal && request.CreatedOn <= agingCutoffUtc) ? 0 : 1));

    public static PendingRequestPage ToPendingPage(this IReadOnlyList<PendingRequestQueueItem> items, bool hasMore)
    {
        var last = hasMore ? items[^1] : null;
        var cursor = last is null
            ? null
            : new PendingQueueCursor(last.EffectivePriorityRank, last.CreatedOn, last.Id);

        return new PendingRequestPage(items, cursor, hasMore);
    }
}
