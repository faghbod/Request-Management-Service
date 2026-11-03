using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Application.IServiceContracts;

// قرارداد آماده‌سازی رویدادها و خواندن تاریخچه درخواست.
public interface IRequestHistoryService
{
    #region ثبت رویدادهای درخواست

    void DiscardPending(int requestId, RequestActivityType eventType);

    Task RecordRegistrationAsync(ServiceRequest request, int userId, CancellationToken cancellationToken = default);

    Task RecordAssignmentAsync(int requestId, int expertId, DateTime occurredAtUtc, CancellationToken cancellationToken = default);

    Task RecordClosureAsync(int requestId, int expertId, int userId, DateTime occurredAtUtc, CancellationToken cancellationToken = default);

    #endregion

    #region دریافت تاریخچه

    Task<IReadOnlyList<RequestHistoryItemDto>> GetRequestHistoryAsync(int requestId, CancellationToken cancellationToken = default);

    Task<ExpertHistoryPage> GetExpertHistoryAsync(int expertId, ExpertHistoryCursor? cursor, int pageSize, CancellationToken cancellationToken = default);

    #endregion
}
