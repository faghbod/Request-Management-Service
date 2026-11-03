using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Application.Mapping;

public static class RequestMapping
{
    public static ServiceRequest ToEntity(this RegisterRequestDto input) => new()
    {
        RequestKey = input.RequestKey,
        Subject = input.Subject,
        Priority = input.Priority,
        Status = RequestStatus.PendingAssignment,
        AssignedExpertId = null,
        AssignedAt = null,
        ClosedAt = null
    };

    public static RequestResponseDto ToResponseDto(this ServiceRequest request) => new(
        request.Id, request.RequestKey, request.Subject, request.Priority, request.Status,
        request.CreatedOn, request.AssignedExpertId);
}
