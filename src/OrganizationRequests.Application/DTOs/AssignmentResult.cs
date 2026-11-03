namespace OrganizationRequests.Application.DTOs;

// نتیجه تلاش برای تخصیص یک درخواست را حمل می‌کند.
public sealed record AssignmentResult(int RequestId, int? ExpertId, AssignmentResultStatus Status);
