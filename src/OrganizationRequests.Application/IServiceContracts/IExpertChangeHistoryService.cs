using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Domain.Entities;

namespace OrganizationRequests.Application.IServiceContracts;

// قرارداد ثبت و خواندن تاریخچه تنظیمات کارشناس.
public interface IExpertChangeHistoryService
{
    #region ثبت تغییرات

    Task RecordCreatedAsync(Expert expert, int actorId, CancellationToken cancellationToken = default);

    Task RecordUpdatedAsync(Expert expert, ExpertConfigurationSnapshot previous, int actorId, CancellationToken cancellationToken = default);

    void DiscardPending(Expert expert);

    #endregion

    #region دریافت تاریخچه

    Task<ExpertChangeHistoryPage> GetExpertChangesAsync(int expertId, ExpertChangeHistoryCursor? cursor, int pageSize, CancellationToken cancellationToken = default);

    #endregion
}
