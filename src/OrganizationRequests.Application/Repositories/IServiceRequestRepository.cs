using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Application.DTOs;

namespace OrganizationRequests.Application.Repositories;

public interface IServiceRequestRepository
{
    Task AddAsync(ServiceRequest request, CancellationToken cancellationToken = default);
    void DiscardAdded(ServiceRequest request);
    Task<ServiceRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ServiceRequest?> GetByRequestKeyAsync(Guid requestKey, CancellationToken cancellationToken = default);
    Task<PendingRequestPage> GetPendingPageAsync(
        DateTime scanStartedAtUtc,
        DateTime agingCutoffUtc,
        int batchSize,
        PendingQueueCursor? cursor,
        CancellationToken cancellationToken = default);
}
