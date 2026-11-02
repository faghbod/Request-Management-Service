using OrganizationRequests.Application.DTOs;

namespace OrganizationRequests.Application.IDtoRepositories;

public interface IPendingRequestDtoRepository
{
    Task<PendingRequestPage> GetPendingPageAsync(
        DateTime scanStartedAtUtc,
        DateTime agingCutoffUtc,
        int batchSize,
        PendingQueueCursor? cursor,
        CancellationToken cancellationToken = default);
}
