using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Application.DTOs;

public sealed record RequestResponseDto(
    int Id,
    Guid RequestKey,
    RequestSubject Subject,
    RequestPriority Priority,
    RequestStatus Status,
    DateTime CreatedOn,
    int? AssignedExpertId);
