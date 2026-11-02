using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OrganizationRequests.Application.Abstractions;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Application.Repositories;
using OrganizationRequests.Application.Services;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;
using OrganizationRequests.Domain.UnitOfWork;
using Xunit;

namespace OrganizationRequests.Tests;

[Trait("Category", "Unit")]
public sealed class RequestClosureServiceTests
{
    [Fact]
    public async Task Closes_once_and_preserves_assignment_and_timestamp()
    {
        var state = new State();
        var assignedAt = state.Request.AssignedAt;
        var service = state.Service();

        var first = await service.CloseAsync(1);
        var second = await service.CloseAsync(1);

        Assert.Equal(RequestClosureResultStatus.Closed, first.Status);
        Assert.Equal(RequestClosureResultStatus.AlreadyClosed, second.Status);
        Assert.Equal(first.ClosedAt, second.ClosedAt);
        Assert.Equal(DateTimeKind.Utc, first.ClosedAt!.Value.Kind);
        Assert.Equal(RequestStatus.Closed, state.Request.Status);
        Assert.Equal(7, state.Request.AssignedExpertId);
        Assert.Equal(assignedAt, state.Request.AssignedAt);
        Assert.Equal(2, state.Expert.ActiveRequestCount);
        Assert.Equal(1, state.Commits);
        var history = Assert.Single(state.History.Items);
        Assert.Equal(7, history.ExpertId);
        Assert.Equal(101, history.ActorUserId);
        Assert.Equal(first.ClosedAt, history.OccurredAtUtc);
    }

    [Theory]
    [InlineData(false, 3, 2)]
    [InlineData(true, 5, 4)]
    public async Task Inactive_or_full_expert_can_release_capacity(bool active, int initialCount, int expectedCount)
    {
        var state = new State();
        state.Expert.IsActive = active;
        state.Expert.IsDeleted = !active;
        state.Expert.ActiveRequestCount = initialCount;
        var result = await state.Service().CloseAsync(1);
        Assert.Equal(RequestClosureResultStatus.Closed, result.Status);
        Assert.Equal(expectedCount, state.Expert.ActiveRequestCount);
    }

    [Fact]
    public async Task Pending_or_missing_requests_do_not_start_transaction()
    {
        var state = new State();
        state.Request.Status = RequestStatus.PendingAssignment;
        Assert.Equal(RequestClosureResultStatus.InvalidState, (await state.Service().CloseAsync(1)).Status);
        Assert.Equal(RequestClosureResultStatus.RequestNotFound, (await state.Service().CloseAsync(99)).Status);
        Assert.Equal(0, state.Transactions);
        Assert.Equal(3, state.Expert.ActiveRequestCount);
    }

    [Fact]
    public async Task Closure_requires_a_real_user_actor()
    {
        var state = new State { UserId = null };
        await Assert.ThrowsAsync<OrganizationRequests.Application.Exceptions.AccessDeniedException>(() => state.Service().CloseAsync(1));
        Assert.Equal(0, state.Transactions);
        Assert.Empty(state.History.Items);
    }
    [Fact]
    public async Task Zero_counter_rolls_back_conditional_update()
    {
        var state = new State();
        state.Expert.ActiveRequestCount = 0;
        var result = await state.Service().CloseAsync(1);
        Assert.Equal(RequestClosureResultStatus.DataIntegrityFailure, result.Status);
        Assert.Equal(RequestStatus.Assigned, state.Request.Status);
        Assert.Null(state.Request.ClosedAt);
        Assert.Equal(0, state.Expert.ActiveRequestCount);
        Assert.Equal(1, state.Rollbacks);
        Assert.Equal(0, state.Commits);
    }

    [Fact]
    public async Task Optimistic_conflict_rolls_back_request_and_counter()
    {
        var state = new State { SaveFailure = new DbUpdateConcurrencyException() };
        var result = await state.Service().CloseAsync(1);
        Assert.Equal(RequestClosureResultStatus.RetryableConflict, result.Status);
        Assert.Equal(RequestStatus.Assigned, state.Request.Status);
        Assert.Null(state.Request.ClosedAt);
        Assert.Equal(3, state.Expert.ActiveRequestCount);
        Assert.Equal(1, state.Rollbacks);
        Assert.Empty(state.History.Items);
    }

    [Fact]
    public async Task Unexpected_failure_is_rolled_back_and_propagated()
    {
        var state = new State { SaveFailure = new ApplicationException("injected failure") };
        await Assert.ThrowsAsync<ApplicationException>(() => state.Service().CloseAsync(1));
        Assert.Equal(RequestStatus.Assigned, state.Request.Status);
        Assert.Null(state.Request.ClosedAt);
        Assert.Equal(3, state.Expert.ActiveRequestCount);
        Assert.Equal(1, state.Rollbacks);
    }

    [Fact]
    public async Task Losing_conditional_update_reads_committed_state_after_rollback()
    {
        var state = new State { LoseConditionalUpdate = true };
        var closedAt = DateTime.UtcNow;
        state.AfterRollback = () => { state.Request.Status = RequestStatus.Closed; state.Request.ClosedAt = closedAt; };
        var result = await state.Service().CloseAsync(1);
        Assert.Equal(RequestClosureResultStatus.AlreadyClosed, result.Status);
        Assert.Equal(closedAt, result.ClosedAt);
        Assert.Equal(3, state.Expert.ActiveRequestCount);
        Assert.Equal(1, state.Rollbacks);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(8)]
    public async Task Wrong_or_missing_expert_cannot_close_or_inspect_assigned_and_closed_states(int? expertId)
    {
        var state = new State { ExpertId = expertId };
        state.Request.CreatedBy = state.UserId!.Value; // Even the request owner cannot close without matching expert identity.
        await Assert.ThrowsAsync<OrganizationRequests.Application.Exceptions.AccessDeniedException>(() => state.Service().CloseAsync(1));
        state.Request.Status = RequestStatus.Closed;
        state.Request.ClosedAt = DateTime.UtcNow;
        await Assert.ThrowsAsync<OrganizationRequests.Application.Exceptions.AccessDeniedException>(() => state.Service().CloseAsync(1));
        Assert.Equal(0, state.Transactions);
        Assert.Empty(state.History.Items);
    }

    [Fact]
    public async Task Access_is_rechecked_after_a_lost_conditional_update()
    {
        var state = new State { LoseConditionalUpdate = true };
        state.AfterRollback = () => { state.Request.AssignedExpertId = 8; state.Request.Status = RequestStatus.Closed; state.Request.ClosedAt = DateTime.UtcNow; };
        await Assert.ThrowsAsync<OrganizationRequests.Application.Exceptions.AccessDeniedException>(() => state.Service().CloseAsync(1));
        Assert.Equal(1, state.Rollbacks);
    }
    private sealed class State : IServiceRequestRepository, IExpertRepository, IUnitOfWork, IPersistenceErrorClassifier, ICurrentUserContext
    {
        public ServiceRequest Request { get; } = new() { Id = 1, Status = RequestStatus.Assigned, AssignedExpertId = 7, AssignedAt = DateTime.UtcNow.AddMinutes(-5) };
        public Expert Expert { get; } = new() { Id = 7, Capacity = 5, ActiveRequestCount = 3, IsActive = true };
        public int Transactions, Commits, Rollbacks;
        public int? UserId { get; set; } = 101;
        public int? ExpertId { get; set; } = 7;
        public TestHistoryRepository History { get; } = new();
        public Exception? SaveFailure;
        public bool LoseConditionalUpdate;
        public Action? AfterRollback;
        public RequestClosureService Service() => new(this, this, this, this, this, new RequestHistoryService(History), NullLogger<RequestClosureService>.Instance);
        public Task<ServiceRequest?> GetForAssignmentAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult<ServiceRequest?>(id == Request.Id && !Request.IsDeleted ? new ServiceRequest
            { Id = Request.Id, Status = Request.Status, AssignedExpertId = Request.AssignedExpertId, AssignedAt = Request.AssignedAt, ClosedAt = Request.ClosedAt } : null);
        public Task<Expert?> GetForClosureAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<Expert?>(id == Expert.Id ? Expert : null);
        public Task<int> TryMarkClosedAsync(int id, int expertId, DateTime closedAtUtc, CancellationToken cancellationToken = default)
        {
            if (LoseConditionalUpdate || Request.Id != id || Request.Status != RequestStatus.Assigned || Request.AssignedExpertId != expertId)
                return Task.FromResult(0);
            Request.Status = RequestStatus.Closed;
            Request.ClosedAt = closedAtUtc;
            return Task.FromResult(1);
        }
        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            Transactions++;
            return Task.FromResult<IUnitOfWorkTransaction>(new Transaction(this));
        }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => SaveFailure is null ? Task.FromResult(1) : Task.FromException<int>(SaveFailure);
        public void DiscardTracking(Expert expert) { }
        public bool IsOptimisticConcurrencyConflict(Exception exception) => exception is DbUpdateConcurrencyException;
        public bool IsRequestKeyUniqueViolation(Exception exception) => false;
        public Task AddAsync(ServiceRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void DiscardAdded(ServiceRequest request) => throw new NotSupportedException();
        public Task<ServiceRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        Task IExpertRepository.AddAsync(Expert expert, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<Expert?> IExpertRepository.GetForManagementAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<Expert?> IExpertRepository.GetManagementViewAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<IReadOnlyList<Expert>> IExpertRepository.GetManagementPageAsync(int? afterId, int take, bool? isActive, RequestSubject? subject, CancellationToken cancellationToken) => throw new NotSupportedException();
        void IExpertRepository.PrepareManagementUpdate(Expert expert, byte[] expectedRowVersion, IReadOnlyCollection<RequestSubject>? specialties) => throw new NotSupportedException();
        void IExpertRepository.DiscardManagementChanges(Expert expert) => throw new NotSupportedException();
        Task<Expert?> IExpertRepository.GetByIdAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ServiceRequest?> GetByRequestKeyAsync(Guid key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> TryMarkAssignedAsync(int requestId, int expertId, DateTime assignedAtUtc, RequestSubject subject, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PendingRequestPage> GetPendingPageAsync(DateTime scan, DateTime cutoff, int size, PendingQueueCursor? cursor, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Expert>> GetEligibleExpertsAsync(RequestSubject subject, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> RevalidateForAssignmentAsync(Expert expert, RequestSubject subject, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        private sealed class Transaction(State state) : IUnitOfWorkTransaction
        {
            private readonly RequestStatus oldStatus = state.Request.Status;
            private readonly DateTime? oldClosedAt = state.Request.ClosedAt;
            private readonly int oldCount = state.Expert.ActiveRequestCount;
            public Task CommitAsync(CancellationToken cancellationToken = default) { state.Commits++; return Task.CompletedTask; }
            public Task RollbackAsync(CancellationToken cancellationToken = default)
            {
                state.Rollbacks++;
                state.Request.Status = oldStatus;
                state.Request.ClosedAt = oldClosedAt;
                state.Expert.ActiveRequestCount = oldCount;
                state.AfterRollback?.Invoke();
                return Task.CompletedTask;
            }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
