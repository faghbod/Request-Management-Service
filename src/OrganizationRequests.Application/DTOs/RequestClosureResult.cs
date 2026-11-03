namespace OrganizationRequests.Application.DTOs;

// نتیجه تلاش برای بستن درخواست را حمل می‌کند.
public sealed record RequestClosureResult(int RequestId, int? ExpertId, DateTime? ClosedAt, RequestClosureResultStatus Status);

// پاسخ HTTP علاوه بر وضعیت، تکراری‌بودن بستن را صریح اعلام می‌کند.
public sealed record RequestClosureResponseDto(int RequestId, int? ExpertId, DateTime? ClosedAt,
    RequestClosureResultStatus Status, bool WasAlreadyClosed);
