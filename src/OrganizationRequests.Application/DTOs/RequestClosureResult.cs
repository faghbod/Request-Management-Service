namespace OrganizationRequests.Application.DTOs;

// نتیجه تلاش برای بستن درخواست را حمل می‌کند.
public sealed record RequestClosureResult(int RequestId, int? ExpertId, DateTime? ClosedAt, RequestClosureResultStatus Status);
