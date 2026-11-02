using Microsoft.EntityFrameworkCore;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Application.Repositories;
using OrganizationRequests.Domain.Entities;
namespace OrganizationRequests.Infrastructure.Persistence.Repositories;
public sealed class ExpertChangeHistoryRepository(ApplicationDbContext db) : IExpertChangeHistoryRepository
{
    public async Task AddAsync(ExpertChangeHistory history, CancellationToken cancellationToken = default) => await db.ExpertChangeHistories.AddAsync(history, cancellationToken);
    public void DiscardPending(Expert expert)
    {
        foreach (var entry in db.ChangeTracker.Entries<ExpertChangeHistory>().Where(x => x.State == EntityState.Added &&
            (ReferenceEquals(x.Entity.Expert, expert) || (expert.Id > 0 && x.Entity.ExpertId == expert.Id))).ToArray())
            entry.State = EntityState.Detached;
    }
    public async Task<(IReadOnlyList<ExpertChangeHistory> Items, bool HasMore)> GetByExpertIdAsync(int expertId, ExpertChangeHistoryCursor? cursor, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = db.ExpertChangeHistories.AsNoTracking().Where(x => x.ExpertId == expertId);
        if (cursor is not null) query = query.Where(x => x.ChangedAtUtc < cursor.ChangedAtUtc || (x.ChangedAtUtc == cursor.ChangedAtUtc && x.Id < cursor.Id));
        var items = await query.OrderByDescending(x => x.ChangedAtUtc).ThenByDescending(x => x.Id).Take(pageSize + 1).ToListAsync(cancellationToken);
        return (items.Take(pageSize).ToArray(), items.Count > pageSize);
    }
}