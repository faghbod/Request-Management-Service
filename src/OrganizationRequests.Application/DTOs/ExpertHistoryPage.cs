namespace OrganizationRequests.Application.DTOs;

// مدل صفحه و نشانگر ادامه تاریخچه فعالیت کارشناس.
public sealed record ExpertHistoryCursor(DateTime OccurredAtUtc, int Id);
public sealed record ExpertHistoryPage(IReadOnlyList<ExpertHistoryItemDto> Items, ExpertHistoryCursor? NextCursor);
