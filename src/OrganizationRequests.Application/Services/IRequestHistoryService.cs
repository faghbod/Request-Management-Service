using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Application.Services;

// قرارداد آماده‌سازی رویدادها و خواندن تاریخچه درخواست.
public interface IRequestHistoryService
{
    void DiscardPending(int requestId, RequestActivityType eventType);
    Task RecordRegistrationAsync(ServiceRequest request, int userId, CancellationToken cancellationToken = default);
    Task RecordAssignmentAsync(int requestId, int expertId, DateTime occurredAtUtc, CancellationToken cancellationToken = default);
    Task RecordClosureAsync(int requestId, int expertId, int userId, DateTime occurredAtUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RequestHistoryItemDto>> GetRequestHistoryAsync(int requestId, CancellationToken cancellationToken = default);
    Task<ExpertHistoryPage> GetExpertHistoryAsync(int expertId, ExpertHistoryCursor? cursor, int pageSize, CancellationToken cancellationToken = default);
}
