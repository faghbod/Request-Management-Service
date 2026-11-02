using Microsoft.Extensions.Options;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Application.Options;
using OrganizationRequests.Application.Repositories;

namespace OrganizationRequests.Application.Services;

public sealed class PendingRequestQueueService(
    IServiceRequestRepository repository,
    IOptions<QueueProcessingOptions> options) : IPendingRequestQueueService
{
    public Task<PendingRequestPage> GetNextPageAsync(
        DateTime scanStartedAtUtc,
        PendingQueueCursor? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (scanStartedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The scan start time must be UTC.", nameof(scanStartedAtUtc));

        var settings = options.Value;
        var agingCutoffUtc = scanStartedAtUtc.AddMinutes(-settings.AgingThresholdMinutes);
        return repository.GetPendingPageAsync(scanStartedAtUtc, agingCutoffUtc, settings.BatchSize, cursor, cancellationToken);
    }
}
