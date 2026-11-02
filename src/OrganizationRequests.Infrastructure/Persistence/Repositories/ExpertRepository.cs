using Microsoft.EntityFrameworkCore;
using OrganizationRequests.Domain.IRepositories;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Infrastructure.Persistence.Repositories;

public sealed class ExpertRepository(ApplicationDbContext dbContext) : IExpertRepository
{
    public async Task AddAsync(Expert expert, CancellationToken cancellationToken = default) =>
        await dbContext.Experts.AddAsync(expert, cancellationToken);

    public Task<Expert?> GetForManagementAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.Experts.AsTracking().Include(x => x.Specialties).SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

    public Task<Expert?> GetManagementViewAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.Experts.AsNoTracking().Include(x => x.Specialties).SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<Expert>> GetManagementPageAsync(int? afterId, int take, bool? isActive, RequestSubject? subject, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Experts.AsNoTracking().Where(x => !x.IsDeleted);
        if (afterId.HasValue) query = query.Where(x => x.Id > afterId.Value);
        if (isActive.HasValue) query = query.Where(x => x.IsActive == isActive.Value);
        if (subject.HasValue) query = query.Where(x => x.Specialties.Any(s => s.Subject == subject.Value));
        return await query.OrderBy(x => x.Id).Take(take).Include(x => x.Specialties).ToListAsync(cancellationToken);
    }

    public void PrepareManagementUpdate(Expert expert, byte[] expectedRowVersion, IReadOnlyCollection<RequestSubject>? specialties)
    {
        var entry = dbContext.Entry(expert);
        if (entry.State == EntityState.Detached) throw new InvalidOperationException("A tracked expert is required.");
        entry.Property(x => x.RowVersion).OriginalValue = expectedRowVersion;
        // Force a parent UPDATE for specialty-only changes, including a RowVersion predicate.
        entry.Property(x => x.Name).IsModified = true;
        if (specialties is null) return;
        foreach (var item in expert.Specialties.Where(x => !specialties.Contains(x.Subject)).ToArray())
        {
            dbContext.ExpertSpecialties.Remove(item);
            expert.Specialties.Remove(item);
        }
        foreach (var subject in specialties.Where(subject => expert.Specialties.All(x => x.Subject != subject)))
            expert.Specialties.Add(new ExpertSpecialty { ExpertId = expert.Id, Expert = expert, Subject = subject });
    }

    public void DiscardManagementChanges(Expert expert)
    {
        foreach (var entry in dbContext.ChangeTracker.Entries<ExpertSpecialty>()
            .Where(x => ReferenceEquals(x.Entity.Expert, expert) || (expert.Id > 0 && x.Entity.ExpertId == expert.Id)).ToArray()) entry.State = EntityState.Detached;
        dbContext.Entry(expert).State = EntityState.Detached;
    }
    public Task<Expert?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.Experts.AsTracking().FirstOrDefaultAsync(expert => expert.Id == id, cancellationToken);

    public Task<Expert?> GetForClosureAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.Experts
            .FromSqlInterpolated($"SELECT * FROM [Experts] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {id}")
            .AsTracking()
            .SingleOrDefaultAsync(cancellationToken);

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
