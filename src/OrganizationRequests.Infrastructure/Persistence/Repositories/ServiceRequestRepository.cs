using Microsoft.EntityFrameworkCore;
using OrganizationRequests.Application.Repositories;
using OrganizationRequests.Domain.Entities;

namespace OrganizationRequests.Infrastructure.Persistence.Repositories;

public sealed class ServiceRequestRepository(ApplicationDbContext dbContext) : IServiceRequestRepository
{
    public async Task AddAsync(ServiceRequest request, CancellationToken cancellationToken = default)
    {
        await dbContext.ServiceRequests.AddAsync(request, cancellationToken);
    }

    public Task<ServiceRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.ServiceRequests
            .AsTracking()
            .FirstOrDefaultAsync(request => request.Id == id && !request.IsDeleted, cancellationToken);

    public Task<ServiceRequest?> GetByRequestKeyAsync(Guid requestKey, CancellationToken cancellationToken = default) =>
        dbContext.ServiceRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(request => request.RequestKey == requestKey, cancellationToken);
}
