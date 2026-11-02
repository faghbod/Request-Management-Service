using Microsoft.EntityFrameworkCore;
using OrganizationRequests.Application.Abstractions;
using OrganizationRequests.Domain.IRepositories;
using OrganizationRequests.Application.IDtoRepositories;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Infrastructure.Persistence.Repositories;

public sealed class ServiceRequestRepository(ApplicationDbContext dbContext, ICurrentUserContext currentUser) : IServiceRequestRepository, IPendingRequestDtoRepository
{
    public async Task AddAsync(ServiceRequest request, CancellationToken cancellationToken = default)
    {
        await dbContext.ServiceRequests.AddAsync(request, cancellationToken);
    }

    public void DiscardAdded(ServiceRequest request)
    {
        foreach (var history in dbContext.ChangeTracker.Entries<RequestActivityHistory>()
            .Where(x => x.State == EntityState.Added && ReferenceEquals(x.Entity.ServiceRequest, request)).ToArray())
            history.State = EntityState.Detached;
        var entry = dbContext.Entry(request);
        if (entry.State == EntityState.Added)
            entry.State = EntityState.Detached;
    }

    public Task<ServiceRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.ServiceRequests
            .AsTracking()
            .FirstOrDefaultAsync(request => request.Id == id && !request.IsDeleted, cancellationToken);

    public Task<ServiceRequest?> GetForAssignmentAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.ServiceRequests.AsNoTracking()
            .FirstOrDefaultAsync(request => request.Id == id && !request.IsDeleted, cancellationToken);

    public Task<int> TryMarkClosedAsync(int requestId, int expertId, DateTime closedAtUtc, CancellationToken cancellationToken = default)
    {
        var actorId = currentUser.UserId ?? 0;
        return dbContext.ServiceRequests
            .Where(request => request.Id == requestId && !request.IsDeleted &&
                request.Status == RequestStatus.Assigned && request.AssignedExpertId == expertId &&
                request.AssignedAt != null && request.AssignedAt <= closedAtUtc && request.ClosedAt == null)
            .ExecuteUpdateAsync(update => update
                .SetProperty(request => request.Status, RequestStatus.Closed)
                .SetProperty(request => request.ClosedAt, closedAtUtc)
                .SetProperty(request => request.ModifiedDate, closedAtUtc)
                .SetProperty(request => request.ModifiedBy, actorId), cancellationToken);
    }

    public Task<int> TryMarkAssignedAsync(
        int requestId, int expertId, DateTime assignedAtUtc, RequestSubject expectedSubject,
        CancellationToken cancellationToken = default)
    {
        var actorId = currentUser.UserId ?? 0;
        return dbContext.ServiceRequests
            .Where(request => request.Id == requestId && !request.IsDeleted &&
                request.Status == RequestStatus.PendingAssignment && request.Subject == expectedSubject &&
                request.AssignedExpertId == null && request.AssignedAt == null && request.ClosedAt == null)
            .ExecuteUpdateAsync(update => update
                .SetProperty(request => request.Status, RequestStatus.Assigned)
                .SetProperty(request => request.AssignedExpertId, expertId)
                .SetProperty(request => request.AssignedAt, assignedAtUtc)
                .SetProperty(request => request.ClosedAt, (DateTime?)null)
                .SetProperty(request => request.ModifiedDate, assignedAtUtc)
                .SetProperty(request => request.ModifiedBy, actorId), cancellationToken);
    }

    public Task<ServiceRequest?> GetByRequestKeyAsync(Guid requestKey, CancellationToken cancellationToken = default) =>
        dbContext.ServiceRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(request => request.RequestKey == requestKey, cancellationToken);

    public async Task<PendingRequestPage> GetPendingPageAsync(
        DateTime scanStartedAtUtc,
        DateTime agingCutoffUtc,
        int batchSize,
        PendingQueueCursor? cursor,
        CancellationToken cancellationToken = default)
    {
        if (batchSize is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(batchSize));

        var query = dbContext.ServiceRequests
            .AsNoTracking()
            .Where(request => request.Status == RequestStatus.PendingAssignment &&
                !request.IsDeleted && request.CreatedOn <= scanStartedAtUtc)
            .Select(request => new
            {
                request.Id,
                request.Subject,
                request.Priority,
                request.CreatedOn,
                EffectivePriorityRank = request.Priority == RequestPriority.Urgent ||
                    (request.Priority == RequestPriority.Normal && request.CreatedOn <= agingCutoffUtc) ? 0 : 1
            });

        if (cursor is not null)
        {
            query = query.Where(item =>
                item.EffectivePriorityRank > cursor.EffectivePriorityRank ||
                (item.EffectivePriorityRank == cursor.EffectivePriorityRank && item.CreatedOn > cursor.CreatedOn) ||
                (item.EffectivePriorityRank == cursor.EffectivePriorityRank && item.CreatedOn == cursor.CreatedOn && item.Id > cursor.Id));
        }

        var items = await query
            .OrderBy(item => item.EffectivePriorityRank)
            .ThenBy(item => item.CreatedOn)
            .ThenBy(item => item.Id)
            .Take(batchSize + 1)
            .Select(item => new PendingRequestQueueItem(
                item.Id, item.Subject, item.Priority, item.CreatedOn, item.EffectivePriorityRank))
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > batchSize;
        if (hasMore)
            items.RemoveAt(batchSize);

        var last = hasMore ? items[^1] : null;
        var nextCursor = last is null ? null : new PendingQueueCursor(last.EffectivePriorityRank, last.CreatedOn, last.Id);
        return new PendingRequestPage(items, nextCursor, hasMore);
    }
}
