using OrganizationRequests.Application.DTOs;

namespace OrganizationRequests.Application.Services;

public interface IPendingRequestQueueService
{
    Task<PendingRequestPage> GetNextPageAsync(
        DateTime scanStartedAtUtc,
        PendingQueueCursor? cursor = null,
        CancellationToken cancellationToken = default);
}
