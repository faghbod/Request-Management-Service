using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Application.Repositories;

public interface IExpertRepository
{
    Task<Expert?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Expert>> GetEligibleExpertsAsync(RequestSubject subject, CancellationToken cancellationToken = default);
}
