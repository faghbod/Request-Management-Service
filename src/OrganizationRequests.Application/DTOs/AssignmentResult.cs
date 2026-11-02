namespace OrganizationRequests.Application.DTOs;

public sealed record AssignmentResult(int RequestId, int? ExpertId, AssignmentResultStatus Status);
