namespace OrganizationRequests.Application.DTOs;

// محتوا و موقعیت ادامه یک صفحه از صف درخواست‌ها.
public sealed record PendingRequestPage(
    IReadOnlyList<PendingRequestQueueItem> Items,
    PendingQueueCursor? NextCursor,
    bool HasMore);
