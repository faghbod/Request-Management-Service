using Microsoft.Extensions.Options;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Application.Options;
using OrganizationRequests.Application.IDtoRepositories;

namespace OrganizationRequests.Application.Services;

// تعیین مرز زمانی صف و دریافت یک صفحه از درخواست‌های منتظر.
public sealed class PendingRequestQueueService(
    IPendingRequestDtoRepository repository,
    IOptions<QueueProcessingOptions> options) : IPendingRequestQueueService
{
    public Task<PendingRequestPage> GetNextPageAsync(
        DateTime scanStartedAtUtc,
        PendingQueueCursor? cursor = null,
        CancellationToken cancellationToken = default)
    {
        #region اعتبارسنجی زمان و محاسبه مرز اولویت

        if (scanStartedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The scan start time must be UTC.", nameof(scanStartedAtUtc));

        var settings = options.Value;
        var agingCutoffUtc = scanStartedAtUtc.AddMinutes(-settings.AgingThresholdMinutes);

        #endregion

        // زمان شروع دور ثابت می‌ماند تا تمام صفحه‌ها با مرز یکسان خوانده شوند.
        return repository.GetPendingPageAsync(scanStartedAtUtc, agingCutoffUtc, settings.BatchSize, cursor, cancellationToken);
    }
}
