using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Application.Mapping;

// تبدیل خروجی صف در خود کوئری؛ فقط ستون‌های موردنیاز DTO از دیتابیس خوانده می‌شوند.
public static class PendingRequestQueueMapping
{
    #region تبدیل کوئری درخواست‌ها به خروجی صف

    // تبدیل تا زمان اجرای کوئری به تعویق می‌افتد و اولویت مؤثر در خروجی محاسبه می‌شود.
    public static IQueryable<PendingRequestQueueItem> ToPendingQueueItems(
        this IQueryable<ServiceRequest> requests, DateTime agingCutoffUtc) =>
        requests.Select(request => new PendingRequestQueueItem(
            request.Id,
            request.Subject,
            request.Priority,
            request.CreatedOn,
            request.Priority == RequestPriority.Urgent ||
            (request.Priority == RequestPriority.Normal && request.CreatedOn <= agingCutoffUtc) ? 0 : 1));

    #endregion

    #region ساخت صفحه و نشانگر ادامه

    // فقط وقتی صفحه دیگری وجود دارد، موقعیت آخرین مورد برای ادامه خواندن برگردانده می‌شود.
    public static PendingRequestPage ToPendingPage(this IReadOnlyList<PendingRequestQueueItem> items, bool hasMore)
    {
        var last = hasMore ? items[^1] : null;
        var cursor = last is null
            ? null
            : new PendingQueueCursor(last.EffectivePriorityRank, last.CreatedOn, last.Id);

        return new PendingRequestPage(items, cursor, hasMore);
    }

    #endregion
}
