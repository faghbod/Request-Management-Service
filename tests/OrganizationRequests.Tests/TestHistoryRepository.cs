using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Domain.IRepositories;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Tests;

// پیاده‌سازی آزمایشی قرارداد تاریخچه برای تست‌ها.
internal sealed class TestHistoryRepository : IRequestActivityHistoryRepository
{
    public List<RequestActivityHistory> Items { get; } = [];
    public void DiscardPending(int requestId, RequestActivityType eventType) => Items.RemoveAll(x => x.ServiceRequestId == requestId && x.EventType == eventType);
    public Task AddAsync(RequestActivityHistory history, CancellationToken cancellationToken = default) { Items.Add(history); return Task.CompletedTask; }
    public Task<IReadOnlyList<RequestActivityHistory>> GetByRequestIdAsync(int requestId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RequestActivityHistory>>(Items.Where(x => x.ServiceRequestId == requestId).OrderBy(x => x.OccurredAtUtc).ThenBy(x => x.Id).ToArray());
    public Task<(IReadOnlyList<RequestActivityHistory> Items, bool HasMore)> GetByExpertIdAsync(int expertId, DateTime? beforeUtc, int? beforeId, int pageSize, CancellationToken cancellationToken = default)
    {
        var matches = Items.Where(x => x.ExpertId == expertId && (!beforeUtc.HasValue || x.OccurredAtUtc < beforeUtc.Value ||
            (x.OccurredAtUtc == beforeUtc.Value && x.Id < beforeId!.Value))).OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.Id).Take(pageSize + 1).ToArray();
        return Task.FromResult< (IReadOnlyList<RequestActivityHistory>, bool)>((matches.Take(pageSize).ToArray(), matches.Length > pageSize));
    }
}
