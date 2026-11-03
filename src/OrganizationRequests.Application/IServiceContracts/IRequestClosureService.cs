using OrganizationRequests.Application.DTOs;

namespace OrganizationRequests.Application.IServiceContracts;

// قرارداد بستن درخواست توسط کارشناس تخصیص‌یافته.
// عملیات داخلی بستن درخواست؛ سرویس دسترسی کارشناس تخصیص‌یافته را بررسی می‌کند.
public interface IRequestClosureService
{
    Task<RequestClosureResult> CloseAsync(int requestId, CancellationToken cancellationToken = default);
}
