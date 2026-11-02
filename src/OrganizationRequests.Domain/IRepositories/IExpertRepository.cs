using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Domain.IRepositories;

public interface IExpertRepository
{
    Task AddAsync(Expert expert, CancellationToken cancellationToken = default);
    Task<Expert?> GetForManagementAsync(int id, CancellationToken cancellationToken = default);
    Task<Expert?> GetManagementViewAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Expert>> GetManagementPageAsync(int? afterId, int take, bool? isActive, RequestSubject? subject, CancellationToken cancellationToken = default);
    void PrepareManagementUpdate(Expert expert, byte[] expectedRowVersion, IReadOnlyCollection<RequestSubject>? specialties);
    void DiscardManagementChanges(Expert expert);
    Task<Expert?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Expert?> GetForClosureAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Expert>> GetEligibleExpertsAsync(RequestSubject subject, CancellationToken cancellationToken = default);
    Task<bool> RevalidateForAssignmentAsync(Expert expert, RequestSubject subject, CancellationToken cancellationToken = default);
    void DiscardTracking(Expert expert);
}
