namespace OrganizationRequests.Application.DTOs;

public sealed record RequestClosureResult(int RequestId, int? ExpertId, DateTime? ClosedAt, RequestClosureResultStatus Status);
