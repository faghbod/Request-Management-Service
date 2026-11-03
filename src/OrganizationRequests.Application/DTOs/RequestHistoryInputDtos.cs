namespace OrganizationRequests.Application.DTOs;

// اطلاعات لازم برای ساخت رویداد تخصیص درخواست.
public sealed record RequestAssignmentHistoryDto(int RequestId, int ExpertId, DateTime OccurredAtUtc);

// اطلاعات لازم برای ساخت رویداد بستن درخواست توسط کارشناس.
public sealed record RequestClosureHistoryDto(int RequestId, int ExpertId, int UserId, DateTime OccurredAtUtc);
