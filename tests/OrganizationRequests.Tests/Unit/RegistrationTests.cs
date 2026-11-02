using Microsoft.Extensions.Logging.Abstractions;
using OrganizationRequests.Application.Abstractions;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Application.Exceptions;
using OrganizationRequests.Application.Repositories;
using OrganizationRequests.Application.Services;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;
using OrganizationRequests.Domain.UnitOfWork;
using Xunit;

namespace OrganizationRequests.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class RegistrationTests
{
    [Fact]
    public async Task Registration_replay_and_conflict_preserve_the_original_state()
    {
        var store = new Store();
        var service = new RequestRegistrationService(store, store, store, store, NullLogger<RequestRegistrationService>.Instance);
        var input = new RegisterRequestDto { RequestKey = Guid.NewGuid(), Subject = RequestSubject.Technical, Priority = RequestPriority.Normal };
        var first = await service.RegisterAsync(input);
        Assert.True(first.IsNewlyCreated);
        Assert.Equal(RequestStatus.PendingAssignment, first.Request.Status);
        Assert.Null(first.Request.AssignedExpertId);
        Assert.Single(store.Records);

        var replay = await service.RegisterAsync(input);
        Assert.False(replay.IsNewlyCreated);
        Assert.Equal(first.Request.Id, replay.Request.Id);
        Assert.Equal(1, store.Saves);

        store.Records[0].Status = RequestStatus.Closed;
        store.Records[0].AssignedExpertId = 7;
        var afterClosure = await service.RegisterAsync(input);
        Assert.Equal(RequestStatus.Closed, afterClosure.Request.Status);
        Assert.Equal(7, afterClosure.Request.AssignedExpertId);
        await Assert.ThrowsAsync<RequestKeyConflictException>(() => service.RegisterAsync(new RegisterRequestDto
        { RequestKey = input.RequestKey, Subject = RequestSubject.Legal, Priority = input.Priority }));
        Assert.Single(store.Records);
        Assert.Equal(1, store.Saves);
    }

    [Fact]
    public async Task Existing_request_key_cannot_be_replayed_by_a_different_user()
    {
        var store = new Store();
        var service = new RequestRegistrationService(store, store, store, store, NullLogger<RequestRegistrationService>.Instance);
        var input = new RegisterRequestDto { RequestKey = Guid.NewGuid(), Subject = RequestSubject.Financial, Priority = RequestPriority.Urgent };
        await service.RegisterAsync(input);
        store.UserId = 2;
        await Assert.ThrowsAsync<RequestKeyConflictException>(() => service.RegisterAsync(input));
        Assert.Single(store.Records);
    }

    private sealed class Store : IServiceRequestRepository, IUnitOfWork, IPersistenceErrorClassifier, ICurrentUserContext
    {
        public List<ServiceRequest> Records { get; } = [];
        public int Saves;
        public int? UserId { get; set; } = 1;
        public Task AddAsync(ServiceRequest request, CancellationToken cancellationToken = default)
        { request.Id = Records.Count + 1; request.CreatedBy = UserId!.Value; request.CreatedOn = DateTime.UtcNow; Records.Add(request); return Task.CompletedTask; }
        public Task<ServiceRequest?> GetByRequestKeyAsync(Guid key, CancellationToken cancellationToken = default) => Task.FromResult(Records.SingleOrDefault(x => x.RequestKey == key));
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { Saves++; return Task.FromResult(1); }
        public void DiscardAdded(ServiceRequest request) => Records.Remove(request);
        public bool IsRequestKeyUniqueViolation(Exception exception) => false;
        public bool IsOptimisticConcurrencyConflict(Exception exception) => false;
        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceRequest?> GetForAssignmentAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> TryMarkClosedAsync(int id, int expertId, DateTime closedAtUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> TryMarkAssignedAsync(int id, int expertId, DateTime assignedAtUtc, RequestSubject subject, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PendingRequestPage> GetPendingPageAsync(DateTime scan, DateTime cutoff, int size, PendingQueueCursor? cursor, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
