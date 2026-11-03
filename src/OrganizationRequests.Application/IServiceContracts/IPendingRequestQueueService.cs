using OrganizationRequests.Application.DTOs;

namespace OrganizationRequests.Application.IServiceContracts;

// قرارداد دریافت صفحه‌ای از صف درخواست‌های منتظر.
public interface IPendingRequestQueueService
{
    Task<PendingRequestPage> GetNextPageAsync(
        DateTime scanStartedAtUtc,
        PendingQueueCursor? cursor = null,
        CancellationToken cancellationToken = default);
}
