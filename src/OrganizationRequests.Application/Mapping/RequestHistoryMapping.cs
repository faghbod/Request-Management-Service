using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Application.Mapping;

// ساخت رویدادهای درخواست و تبدیل تاریخچه به مدل‌های خروجی.
public static class RequestHistoryMapping
{
    #region ساخت موجودیت تاریخچه

    // ثبت اولیه به درخواست تازه و کاربر ثبت‌کننده مرتبط است.
    public static RequestActivityHistory ToRegistrationHistory(this ServiceRequest request, int userId) => new()
    {
        // نگهداری ارتباط با همان موجودیت برای ثبت کلید خارجی و زمان حسابرسی ضروری است.
        ServiceRequest = request,
        EventType = RequestActivityType.Registered,
        NewStatus = RequestStatus.PendingAssignment,
        ActorType = HistoryActorType.User,
        ActorUserId = userId,
        OccurredAtUtc = request.CreatedOn,
        Description = "Request registered"
    };

    // تخصیص یک رویداد سیستمی است و کاربر انسانی به‌عنوان عامل ندارد.
    public static RequestActivityHistory ToEntity(this RequestAssignmentHistoryDto input) => new()
    {
        ServiceRequestId = input.RequestId,
        ExpertId = input.ExpertId,
        EventType = RequestActivityType.Assigned,
        PreviousStatus = RequestStatus.PendingAssignment,
        NewStatus = RequestStatus.Assigned,
        ActorType = HistoryActorType.System,
        OccurredAtUtc = input.OccurredAtUtc,
        Description = "Request assigned to expert"
    };

    // در رویداد بستن، شناسه کارشناس و شناسه کاربر اقدام‌کننده جدا نگهداری می‌شوند.
    public static RequestActivityHistory ToEntity(this RequestClosureHistoryDto input) => new()
    {
        ServiceRequestId = input.RequestId,
        ExpertId = input.ExpertId,
        EventType = RequestActivityType.Closed,
        PreviousStatus = RequestStatus.Assigned,
        NewStatus = RequestStatus.Closed,
        ActorType = HistoryActorType.User,
        ActorUserId = input.UserId,
        OccurredAtUtc = input.OccurredAtUtc,
        Description = "Request closed"
    };

    #endregion

    #region تبدیل تاریخچه به خروجی

    // زمان ذخیره‌شده به‌عنوان UTC در خروجی تاریخچه مشخص می‌شود.
    public static RequestHistoryItemDto ToRequestHistoryDto(this RequestActivityHistory item) => new(
        item.EventType, item.PreviousStatus, item.NewStatus, item.ExpertId, item.ActorType,
        DateTime.SpecifyKind(item.OccurredAtUtc, DateTimeKind.Utc));

    // خروجی تاریخچه کارشناس، موضوع درخواست مرتبط را نیز نمایش می‌دهد.
    public static ExpertHistoryItemDto ToExpertHistoryDto(this RequestActivityHistory item) => new(
        item.Id, item.ServiceRequestId, item.ServiceRequest.Subject, item.EventType, item.PreviousStatus,
        item.NewStatus, item.ActorType, item.ActorUserId,
        DateTime.SpecifyKind(item.OccurredAtUtc, DateTimeKind.Utc), item.Description);

    // نشانگر صفحه بعد از آخرین رویداد همین صفحه ساخته می‌شود.
    public static ExpertHistoryPage ToExpertHistoryPage(this IReadOnlyList<RequestActivityHistory> items, bool hasMore)
    {
        var result = items.Select(item => item.ToExpertHistoryDto()).ToArray();
        var last = result.LastOrDefault();
        var cursor = hasMore && last is not null
            ? new ExpertHistoryCursor(last.OccurredAtUtc, last.Id)
            : null;

        return new ExpertHistoryPage(result, cursor);
    }

    #endregion
}
