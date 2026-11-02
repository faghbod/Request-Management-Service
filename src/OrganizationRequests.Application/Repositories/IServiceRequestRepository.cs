using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Application.Repositories;

public interface IServiceRequestRepository
{
    Task AddAsync(ServiceRequest request, CancellationToken cancellationToken = default);
    void DiscardAdded(ServiceRequest request);
    Task<ServiceRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ServiceRequest?> GetForAssignmentAsync(int id, CancellationToken cancellationToken = default);
    Task<int> TryMarkClosedAsync(int requestId, int expertId, DateTime closedAtUtc, CancellationToken cancellationToken = default);
    Task<ServiceRequest?> GetByRequestKeyAsync(Guid requestKey, CancellationToken cancellationToken = default);
    Task<int> TryMarkAssignedAsync(
        int requestId,
        int expertId,
        DateTime assignedAtUtc,
        RequestSubject expectedSubject,
        CancellationToken cancellationToken = default);
    Task<PendingRequestPage> GetPendingPageAsync(
        DateTime scanStartedAtUtc,
        DateTime agingCutoffUtc,
        int batchSize,
        PendingQueueCursor? cursor,
        CancellationToken cancellationToken = default);
}
