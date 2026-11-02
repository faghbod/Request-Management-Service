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

    public async Task<bool> RevalidateForAssignmentAsync(
        Expert expert, RequestSubject subject, CancellationToken cancellationToken = default)
    {
        var entry = dbContext.Entry(expert);
        if (entry.State == EntityState.Detached)
            return false;

        await entry.ReloadAsync(cancellationToken);
        if (entry.State == EntityState.Detached || !expert.IsActive || expert.IsDeleted ||
            expert.Capacity <= 0 || expert.ActiveRequestCount < 0 || expert.ActiveRequestCount >= expert.Capacity)
            return false;

        // Keep the matching specialty stable until this transaction completes.
        return await dbContext.ExpertSpecialties
            .FromSqlInterpolated($"SELECT [ExpertId], [Subject] FROM [ExpertSpecialties] WITH (HOLDLOCK) WHERE [ExpertId] = {expert.Id} AND [Subject] = {(int)subject}")
            .AsNoTracking()
            .AnyAsync(cancellationToken);
    }

    public void DiscardTracking(Expert expert) => dbContext.Entry(expert).State = EntityState.Detached;
}
