using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Domain.IRepositories;

// قرارداد دسترسی و تغییر وضعیت موجودیت درخواست.
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
}
