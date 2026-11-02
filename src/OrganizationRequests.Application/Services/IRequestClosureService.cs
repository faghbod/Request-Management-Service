using OrganizationRequests.Application.DTOs;

namespace OrganizationRequests.Application.Services;

// Internal business operation. The service enforces assigned-expert authorization.
public interface IRequestClosureService
{
    Task<RequestClosureResult> CloseAsync(int requestId, CancellationToken cancellationToken = default);
}
