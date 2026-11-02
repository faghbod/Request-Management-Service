using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Application.Services;
using OrganizationRequests.Domain.Entities.Enums;
using OrganizationRequests.Worker.Options;
using OrganizationRequests.Worker.Workers;
using Xunit;

namespace OrganizationRequests.Tests;

public sealed class AssignmentWorkerTests
{
    [Fact]
    public async Task Processes_all_pages_without_head_of_line_blocking_and_keeps_scan_time()
    {
        var state = new TestState();
        var first = Enumerable.Range(1, 50).Select(Item).ToArray();
        var cursor = new PendingQueueCursor(0, first[^1].CreatedOn, first[^1].Id);
        state.Pages.Enqueue(new(first, cursor, true));
        state.Pages.Enqueue(new([Item(51)], null, false));
        state.Assign = (id, _) => Task.FromResult(id == 51 ? AssignmentResultStatus.Assigned : AssignmentResultStatus.NoEligibleExpert);
        await using var fixture = new Fixture(state);

        var stats = await fixture.Worker.ProcessQueueCycleAsync(CancellationToken.None);

        Assert.Equal(2, stats.Pages);
        Assert.Equal(51, stats.Inspected);
        Assert.Equal(50, stats.Unavailable);
        Assert.Equal(1, stats.Assigned);
        Assert.Equal(51, state.Attempts.Count);
        Assert.Null(state.Reads[0].Cursor);
        Assert.Equal(cursor, state.Reads[1].Cursor);
        Assert.Equal(state.Reads[0].At, state.Reads[1].At);
        Assert.Equal(DateTimeKind.Utc, state.Reads[0].At.Kind);
        Assert.Equal(53, state.Disposed);
    }

    [Fact]
    public async Task Retries_conflict_with_fresh_scopes_and_counts_outcomes()
    {
        var state = new TestState();
        state.Pages.Enqueue(Page(1, 2, 3, 4));
        var calls = 0;
        state.Assign = (id, _) => Task.FromResult(id switch
        {
            1 when calls++ == 0 => AssignmentResultStatus.RetryableConflict,
            1 => AssignmentResultStatus.Assigned,
            2 => AssignmentResultStatus.AlreadyProcessed,
            3 => AssignmentResultStatus.RequestNotFound,
            _ => AssignmentResultStatus.NoEligibleExpert
        });
        await using var fixture = new Fixture(state);

        var stats = await fixture.Worker.ProcessQueueCycleAsync(CancellationToken.None);

        Assert.Equal(1, stats.Assigned);
        Assert.Equal(1, stats.Retries);
        Assert.Equal(1, stats.AlreadyProcessed);
        Assert.Equal(1, stats.NotFound);
        Assert.Equal(1, stats.Unavailable);
        Assert.Equal(5, state.Attempts.Count);
        Assert.Equal(5, state.Attempts.Select(a => a.ScopeId).Distinct().Count());
        Assert.Equal(6, state.Disposed);
    }

    [Fact]
    public async Task Exhausts_bounded_retries_then_processes_next_request()
    {
        var state = new TestState();
        state.Pages.Enqueue(Page(1, 2));
        state.Assign = (id, _) => Task.FromResult(id == 1 ? AssignmentResultStatus.RetryableConflict : AssignmentResultStatus.Assigned);
        await using var fixture = new Fixture(state);

        var stats = await fixture.Worker.ProcessQueueCycleAsync(CancellationToken.None);

        Assert.Equal(3, state.Attempts.Count(x => x.RequestId == 1));
        Assert.Equal(2, stats.Retries);
        Assert.Equal(1, stats.Conflicts);
        Assert.Equal(1, stats.Assigned);
        Assert.Equal(4, state.Attempts.Select(a => a.ScopeId).Distinct().Count());
    }

    [Fact]
    public async Task Cancellation_stops_new_attempts_and_disposes_scopes()
    {
        using var cts = new CancellationTokenSource();
        var state = new TestState();
        state.Pages.Enqueue(Page(1, 2));
        state.Assign = (_, token) =>
        {
            Assert.Equal(cts.Token, token);
            cts.Cancel();
            return Task.FromResult(AssignmentResultStatus.RetryableConflict);
        };
        await using var fixture = new Fixture(state);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Worker.ProcessQueueCycleAsync(cts.Token));
        Assert.Single(state.Attempts);
        Assert.Equal(2, state.Disposed);
    }

    [Fact]
    public async Task Worker_does_not_overlap_cycles_and_stops_cleanly()
    {
        var state = new TestState();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        state.ReadPage = async token =>
        {
            started.TrySetResult();
            await release.Task.WaitAsync(token);
            return Page();
        };
        await using var fixture = new Fixture(state, pollingSeconds: 1);
        await fixture.Worker.StartAsync(CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(1200);
        Assert.Single(state.Reads);
        release.SetResult();
        await fixture.Worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single(state.Reads);
    }

    private static PendingRequestQueueItem Item(int id) => new(id, RequestSubject.Legal, RequestPriority.Normal, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 0);
    private static PendingRequestPage Page(params int[] ids) => new(ids.Select(Item).ToArray(), null, false);

    private sealed class TestState
    {
        public Queue<PendingRequestPage> Pages { get; } = new();
        public List<(DateTime At, PendingQueueCursor? Cursor)> Reads { get; } = [];
        public List<(int RequestId, Guid ScopeId)> Attempts { get; } = [];
        public int Disposed;
        public Func<int, CancellationToken, Task<AssignmentResultStatus>> Assign { get; set; } = (_, _) => Task.FromResult(AssignmentResultStatus.Assigned);
        public Func<CancellationToken, Task<PendingRequestPage>>? ReadPage { get; set; }
    }

    private sealed class ScopeMarker(TestState state) : IDisposable
    {
        public Guid Id { get; } = Guid.NewGuid();
        public void Dispose() => state.Disposed++;
    }

    private sealed class QueueDouble(TestState state, ScopeMarker marker) : IPendingRequestQueueService
    {
        public async Task<PendingRequestPage> GetNextPageAsync(DateTime scanStartedAtUtc, PendingQueueCursor? cursor = null, CancellationToken cancellationToken = default)
        {
            _ = marker.Id;
            state.Reads.Add((scanStartedAtUtc, cursor));
            return state.ReadPage is null ? state.Pages.Dequeue() : await state.ReadPage(cancellationToken);
        }
    }

    private sealed class AssignmentDouble(TestState state, ScopeMarker marker) : IRequestAssignmentService
    {
        public async Task<AssignmentResult> TryAssignAsync(int requestId, CancellationToken cancellationToken = default)
        {
            state.Attempts.Add((requestId, marker.Id));
            return new(requestId, null, await state.Assign(requestId, cancellationToken));
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider provider;
        public AssignmentWorker Worker { get; }
        public Fixture(TestState state, int pollingSeconds = 30)
        {
            var services = new ServiceCollection();
            services.AddSingleton(state);
            services.AddScoped<ScopeMarker>();
            services.AddScoped<IPendingRequestQueueService, QueueDouble>();
            services.AddScoped<IRequestAssignmentService, AssignmentDouble>();
            provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            Worker = new(provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(new AssignmentWorkerOptions
            {
                PollingIntervalSeconds = pollingSeconds, InitialRetryDelayMilliseconds = 0, MaxRetryDelayMilliseconds = 0
            }), NullLogger<AssignmentWorker>.Instance);
        }
        public async ValueTask DisposeAsync()
        {
            Worker.Dispose();
            await provider.DisposeAsync();
        }
    }
}
