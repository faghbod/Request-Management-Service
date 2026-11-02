using System.Diagnostics;
using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Application.Services;
using OrganizationRequests.Worker.Options;

namespace OrganizationRequests.Worker.Workers;

public sealed class AssignmentWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<AssignmentWorkerOptions> options,
    ILogger<AssignmentWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Assignment worker started.");
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessQueueCycleAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception) when (exception is DbException or TimeoutException)
                {
                    logger.LogError(exception, "Assignment cycle failed; the next cycle will start after the polling delay.");
                }
                catch (Exception exception)
                {
                    logger.LogCritical(exception, "Assignment cycle failed with an unrecoverable error.");
                    throw;
                }

                await DelayPollingAsync(options.Value.PollingIntervalSeconds, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (stoppingToken.IsCancellationRequested)
                logger.LogInformation("Assignment worker shutdown requested.");
            logger.LogInformation("Assignment worker stopped.");
        }
    }

    public async Task<AssignmentCycleStatistics> ProcessQueueCycleAsync(CancellationToken cancellationToken)
    {
        var stats = new AssignmentCycleStatistics();
        var watch = Stopwatch.StartNew();
        var scanStartedAtUtc = DateTime.UtcNow;
        PendingQueueCursor? cursor = null;
        logger.LogInformation("Assignment cycle started at {ScanStartedAtUtc}.", scanStartedAtUtc);
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PendingRequestPage page;
                await using (var scope = scopeFactory.CreateAsyncScope())
                {
                    var queue = scope.ServiceProvider.GetRequiredService<IPendingRequestQueueService>();
                    page = await queue.GetNextPageAsync(scanStartedAtUtc, cursor, cancellationToken);
                }
                stats.Pages++;

                foreach (var item in page.Items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    stats.Inspected++;
                    try
                    {
                        await ProcessRequestAsync(item.Id, stats, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        // Stop this cycle on an unexpected failure, especially a database outage.
                        stats.Errors++;
                        logger.LogError(exception, "Assignment failed for request {RequestId}; aborting cycle.", item.Id);
                        throw;
                    }
                }

                if (!page.HasMore)
                    break;
                cursor = page.NextCursor ?? throw new InvalidOperationException("Queue returned HasMore without a cursor.");
            }

            logger.LogInformation("Assignment cycle completed in {DurationMs} ms: pages {Pages}, inspected {Inspected}, assigned {Assigned}, already processed {AlreadyProcessed}, unavailable {Unavailable}, not found {NotFound}, retries {Retries}, exhausted conflicts {Conflicts}, errors {Errors}.",
                watch.ElapsedMilliseconds, stats.Pages, stats.Inspected, stats.Assigned, stats.AlreadyProcessed,
                stats.Unavailable, stats.NotFound, stats.Retries, stats.Conflicts, stats.Errors);
            return stats;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            if (stats.Errors == 0)
                stats.Errors++;
            logger.LogWarning("Assignment cycle ended after {DurationMs} ms: pages {Pages}, inspected {Inspected}, assigned {Assigned}, already processed {AlreadyProcessed}, unavailable {Unavailable}, not found {NotFound}, retries {Retries}, exhausted conflicts {Conflicts}, errors {Errors}.",
                watch.ElapsedMilliseconds, stats.Pages, stats.Inspected, stats.Assigned, stats.AlreadyProcessed,
                stats.Unavailable, stats.NotFound, stats.Retries, stats.Conflicts, stats.Errors);
            throw;
        }
    }

    private static async Task DelayPollingAsync(int seconds, CancellationToken cancellationToken)
    {
        long remaining = (long)seconds * 1000;
        while (remaining > 0)
        {
            var delay = (int)Math.Min(remaining, int.MaxValue);
            await Task.Delay(delay, cancellationToken);
            remaining -= delay;
        }
    }

    private async Task ProcessRequestAsync(int requestId, AssignmentCycleStatistics stats, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        long delay = settings.InitialRetryDelayMilliseconds;
        for (var attempt = 1; attempt <= settings.MaxAssignmentAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AssignmentResult result;
            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<IRequestAssignmentService>();
                result = await service.TryAssignAsync(requestId, cancellationToken);
            }

            switch (result.Status)
            {
                case AssignmentResultStatus.Assigned: stats.Assigned++; return;
                case AssignmentResultStatus.AlreadyProcessed: stats.AlreadyProcessed++; return;
                case AssignmentResultStatus.NoEligibleExpert: stats.Unavailable++; return;
                case AssignmentResultStatus.RequestNotFound: stats.NotFound++; return;
                case AssignmentResultStatus.RetryableConflict:
                    if (attempt == settings.MaxAssignmentAttempts)
                    {
                        stats.Conflicts++;
                        logger.LogWarning("Assignment conflict for request {RequestId} remains after {Attempts} attempts.", requestId, attempt);
                        return;
                    }
                    stats.Retries++;
                    await Task.Delay(TimeSpan.FromMilliseconds(delay), cancellationToken);
                    delay = Math.Min(delay * 2, settings.MaxRetryDelayMilliseconds);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown assignment result: {result.Status}.");
            }
        }
    }
}

public sealed class AssignmentCycleStatistics
{
    public int Pages { get; internal set; }
    public int Inspected { get; internal set; }
    public int Assigned { get; internal set; }
    public int AlreadyProcessed { get; internal set; }
    public int Unavailable { get; internal set; }
    public int NotFound { get; internal set; }
    public int Retries { get; internal set; }
    public int Conflicts { get; internal set; }
    public int Errors { get; internal set; }
}
