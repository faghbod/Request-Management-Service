namespace OrganizationRequests.Application.DTOs;

// موقعیت آخرین مورد برای ادامه خواندن صف در صفحه بعد.
public sealed record PendingQueueCursor(int EffectivePriorityRank, DateTime CreatedOn, int Id);
