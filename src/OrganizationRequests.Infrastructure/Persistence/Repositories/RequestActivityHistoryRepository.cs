using Microsoft.EntityFrameworkCore;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Domain.IRepositories;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Infrastructure.Persistence.Repositories;

// پیاده‌سازی ثبت و خواندن تاریخچه رویدادهای درخواست.
public sealed class RequestActivityHistoryRepository(ApplicationDbContext dbContext) : IRequestActivityHistoryRepository
{
    public async Task AddAsync(RequestActivityHistory history, CancellationToken cancellationToken = default) =>
        await dbContext.RequestActivityHistories.AddAsync(history, cancellationToken);

    // پس از برگشت تراکنش فقط رویدادهای ذخیره‌نشده از ردیابی خارج می‌شوند.
    public void DiscardPending(int requestId, RequestActivityType eventType)
    {
        foreach (var entry in dbContext.ChangeTracker.Entries<RequestActivityHistory>()
            .Where(x => x.State == EntityState.Added && x.Entity.ServiceRequestId == requestId && x.Entity.EventType == eventType).ToArray())
            entry.State = EntityState.Detached;
    }
    public async Task<IReadOnlyList<RequestActivityHistory>> GetByRequestIdAsync(int requestId, CancellationToken cancellationToken = default) =>
        await dbContext.RequestActivityHistories.AsNoTracking().Where(x => x.ServiceRequestId == requestId)
            .OrderBy(x => x.OccurredAtUtc).ThenBy(x => x.Id).ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<RequestActivityHistory> Items, bool HasMore)> GetByExpertIdAsync(
        int expertId, DateTime? beforeUtc, int? beforeId, int pageSize, CancellationToken cancellationToken = default)
    {
        if (pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));
        if (beforeUtc.HasValue != beforeId.HasValue) throw new ArgumentException("Both cursor values are required.");
        var query = dbContext.RequestActivityHistories.AsNoTracking().Include(x => x.ServiceRequest).Where(x => x.ExpertId == expertId);
        if (beforeUtc.HasValue)
            query = query.Where(x => x.OccurredAtUtc < beforeUtc.Value || (x.OccurredAtUtc == beforeUtc.Value && x.Id < beforeId!.Value));
        var items = await query.OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.Id).Take(pageSize + 1).ToListAsync(cancellationToken);
        var hasMore = items.Count > pageSize;
        if (hasMore) items.RemoveAt(items.Count - 1);
        return (items, hasMore);
    }
}
