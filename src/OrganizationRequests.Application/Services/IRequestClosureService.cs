using OrganizationRequests.Application.DTOs;

namespace OrganizationRequests.Application.Services;

// Internal business operation. Callers must authorize access before invoking it.
public interface IRequestClosureService
{
    Task<RequestClosureResult> CloseAsync(int requestId, CancellationToken cancellationToken = default);
}
