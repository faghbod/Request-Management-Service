using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Domain.IRepositories;

// قرارداد ثبت و خواندن رویدادهای تاریخچه درخواست.
public interface IRequestActivityHistoryRepository
{
    Task AddAsync(RequestActivityHistory history, CancellationToken cancellationToken = default);
    void DiscardPending(int requestId, RequestActivityType eventType);
    Task<IReadOnlyList<RequestActivityHistory>> GetByRequestIdAsync(int requestId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<RequestActivityHistory> Items, bool HasMore)> GetByExpertIdAsync(int expertId, DateTime? beforeUtc, int? beforeId, int pageSize, CancellationToken cancellationToken = default);
}
