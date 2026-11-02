using OrganizationRequests.Application.DTOs;

namespace OrganizationRequests.Application.Services;

public interface IRequestAssignmentService
{
    Task<AssignmentResult> TryAssignAsync(int requestId, CancellationToken cancellationToken = default);
}
