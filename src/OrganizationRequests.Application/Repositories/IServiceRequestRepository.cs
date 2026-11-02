using OrganizationRequests.Domain.Entities;

namespace OrganizationRequests.Application.Repositories;

public interface IServiceRequestRepository
{
    Task AddAsync(ServiceRequest request, CancellationToken cancellationToken = default);
    Task<ServiceRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ServiceRequest?> GetByRequestKeyAsync(Guid requestKey, CancellationToken cancellationToken = default);
}
