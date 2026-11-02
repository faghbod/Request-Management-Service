using Microsoft.EntityFrameworkCore;
using OrganizationRequests.Application.Repositories;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Infrastructure.Persistence.Repositories;

public sealed class ExpertRepository(ApplicationDbContext dbContext) : IExpertRepository
{
    public Task<Expert?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.Experts.AsTracking().FirstOrDefaultAsync(expert => expert.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Expert>> GetEligibleExpertsAsync(
        RequestSubject subject,
        CancellationToken cancellationToken = default) =>
        await dbContext.Experts
            .AsTracking()
            .Where(expert => expert.IsActive && !expert.IsDeleted &&
                expert.Capacity > 0 && expert.ActiveRequestCount < expert.Capacity &&
                expert.Specialties.Any(specialty => specialty.Subject == subject))
            .OrderBy(expert => expert.Id)
            .ToListAsync(cancellationToken);
}
