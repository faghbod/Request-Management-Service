using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Application.Repositories;

public interface IExpertRepository
{
    Task<Expert?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Expert?> GetForClosureAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Expert>> GetEligibleExpertsAsync(RequestSubject subject, CancellationToken cancellationToken = default);
    Task<bool> RevalidateForAssignmentAsync(Expert expert, RequestSubject subject, CancellationToken cancellationToken = default);
    void DiscardTracking(Expert expert);
}
