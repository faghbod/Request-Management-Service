using Microsoft.EntityFrameworkCore;
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
public sealed class ExpertManagementTests
{
    private const string Version = "AQIDBAUGBwg=";
    private static CreateExpertDto Create(string? name = "  Example  ", int capacity = 5, bool? active = true, params RequestSubject[] subjects) =>
        new() { Name = name, Capacity = capacity, IsActive = active, Specialties = subjects };
    private static UpdateExpertDto Update(int capacity, bool active, params RequestSubject[] subjects) =>
        new() { Name = "  Renamed  ", Capacity = capacity, IsActive = active, Specialties = subjects, ExpectedRowVersion = Version };

    [Fact]
    public async Task Creates_expert_with_trimmed_name_shared_capacity_and_no_active_requests()
    {
        var store = new Store();
        var result = await store.Service.CreateExpertAsync(Create(subjects: [RequestSubject.Legal, RequestSubject.Financial]));
        Assert.Equal("Example", result.Name);
        Assert.Equal(0, result.ActiveRequestCount);
        Assert.Equal(5, result.AvailableCapacity);
        Assert.Equal(new[] { RequestSubject.Financial, RequestSubject.Legal }, result.Specialties);
        Assert.True(result.IsActive);
        Assert.Equal(1, store.Saves);
    }

    [Theory]
    [InlineData(null, 1)]
    [InlineData(" ", 1)]
    [InlineData("ok", 0)]
    [InlineData("ok", -1)]
    public async Task Invalid_name_or_capacity_is_rejected(string? name, int capacity)
    {
        var store = new Store();
        await Assert.ThrowsAsync<ExpertValidationException>(() => store.Service.CreateExpertAsync(Create(name, capacity, subjects: [RequestSubject.Legal])));
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public async Task Invalid_specialties_and_missing_active_status_are_rejected()
    {
        var store = new Store();
        await Assert.ThrowsAsync<ExpertValidationException>(() => store.Service.CreateExpertAsync(Create(new string('x', 151), 1, true, RequestSubject.Legal)));
        await Assert.ThrowsAsync<ExpertValidationException>(() => store.Service.CreateExpertAsync(Create("ok", 1, true)));
        await Assert.ThrowsAsync<ExpertValidationException>(() => store.Service.CreateExpertAsync(Create("ok", 1, true, (RequestSubject)99)));
        await Assert.ThrowsAsync<ExpertValidationException>(() => store.Service.CreateExpertAsync(Create("ok", 1, true, RequestSubject.Legal, RequestSubject.Legal)));
        await Assert.ThrowsAsync<ExpertValidationException>(() => store.Service.CreateExpertAsync(Create("ok", 1, null, RequestSubject.Legal)));
        Assert.False((await store.Service.CreateExpertAsync(Create("ok", 1, false))).IsActive);
    }

    [Fact]
    public async Task Capacity_and_specialties_preserve_active_count_and_assignments()
    {
        var store = Store.Existing();
        var request = new ServiceRequest { Id = 55, AssignedExpertId = 7 };
        store.Expert!.AssignedRequests.Add(request);
        var result = await store.Service.UpdateExpertAsync(7, Update(8, true, RequestSubject.Legal, RequestSubject.Technical));
        Assert.Equal(3, result.ActiveRequestCount);
        Assert.Equal(5, result.AvailableCapacity);
        Assert.Equal(7, request.AssignedExpertId);
        Assert.Equal(1, store.Prepared);
        Assert.Equal(Convert.FromBase64String(Version), store.Expected);
        Assert.Equal(1, store.Commits);
        result = await store.Service.UpdateExpertAsync(7, Update(3, true, RequestSubject.Financial));
        Assert.Equal(3, result.Capacity);
        Assert.Equal(3, result.ActiveRequestCount);
        Assert.Equal(7, request.AssignedExpertId);
        await Assert.ThrowsAsync<ExpertValidationException>(() => store.Service.UpdateExpertAsync(7, Update(2, true, RequestSubject.Financial)));
        await Assert.ThrowsAsync<ExpertValidationException>(() => store.Service.UpdateExpertAsync(7, Update(5, true)));
        Assert.Equal(3, store.Expert.ActiveRequestCount);
    }

    [Fact]
    public async Task Deactivate_and_reactivate_preserves_specialties_and_requests()
    {
        var store = Store.Existing();
        var request = new ServiceRequest { AssignedExpertId = 7 };
        store.Expert!.AssignedRequests.Add(request);
        var result = await store.Service.SetExpertActiveStatusAsync(7, new() { IsActive = false, ExpectedRowVersion = Version });
        Assert.False(result.IsActive);
        Assert.Equal(3, result.ActiveRequestCount);
        Assert.Equal(7, request.AssignedExpertId);
        Assert.Single(result.Specialties);
        Assert.True((await store.Service.SetExpertActiveStatusAsync(7, new() { IsActive = true, ExpectedRowVersion = Version })).IsActive);
        store.Expert.Specialties.Clear();
        await Assert.ThrowsAsync<ExpertValidationException>(() => store.Service.SetExpertActiveStatusAsync(7, new() { IsActive = true, ExpectedRowVersion = Version }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-base64")]
    [InlineData("AQ==")]
    public async Task Invalid_or_missing_rowversion_is_rejected(string? token)
    {
        var store = Store.Existing();
        var input = Update(5, true, RequestSubject.Legal);
        input.ExpectedRowVersion = token;
        await Assert.ThrowsAsync<ExpertValidationException>(() => store.Service.UpdateExpertAsync(7, input));
        Assert.Equal(0, store.Prepared);
    }

    [Fact]
    public async Task Stale_version_and_database_concurrency_conflict_are_typed()
    {
        var store = Store.Existing();
        var input = Update(5, true, RequestSubject.Legal);
        input.ExpectedRowVersion = Convert.ToBase64String(new byte[8]);
        await Assert.ThrowsAsync<ExpertConcurrencyException>(() => store.Service.UpdateExpertAsync(7, input));
        Assert.Equal(0, store.Prepared);
        input.ExpectedRowVersion = Version;
        store.Failure = new DbUpdateConcurrencyException();
        await Assert.ThrowsAsync<ExpertConcurrencyException>(() => store.Service.UpdateExpertAsync(7, input));
        Assert.Equal(1, store.Rollbacks);
        Assert.Equal(0, store.Commits);
        Assert.True(store.Discarded);
    }

    [Fact]
    public async Task Unknown_id_and_invalid_page_are_rejected()
    {
        var store = Store.Existing();
        await Assert.ThrowsAsync<ResourceNotFoundException>(() => store.Service.GetExpertByIdAsync(8));
        await Assert.ThrowsAsync<ResourceNotFoundException>(() => store.Service.SetExpertActiveStatusAsync(8, new() { IsActive = false, ExpectedRowVersion = Version }));
        await Assert.ThrowsAsync<ExpertValidationException>(() => store.Service.GetExpertsAsync(null, 101, null, null));
    }

    [Fact]
    public async Task Failed_creation_discards_pending_expert()
    {
        var store = new Store { Failure = new ApplicationException("injected") };
        await Assert.ThrowsAsync<ApplicationException>(() => store.Service.CreateExpertAsync(Create(subjects: [RequestSubject.Legal])));
        Assert.True(store.Discarded);
    }

    [Fact]
    public async Task Listing_is_bounded_and_returns_next_cursor()
    {
        var store = Store.Existing();
        store.Page = Enumerable.Range(1, 4).Select(id => new Expert { Id = id, Name = $"E{id}", Capacity = 5,
            IsActive = true, RowVersion = Convert.FromBase64String(Version) }).ToArray();
        var page = await store.Service.GetExpertsAsync(null, 2, true, RequestSubject.Technical);
        Assert.Equal(new[] { 1, 2 }, page.Items.Select(x => x.Id));
        Assert.Equal(2, page.NextAfterId);
        Assert.Equal(3, store.Take);
        Assert.True(store.FilterActive);
        Assert.Equal(RequestSubject.Technical, store.FilterSubject);
        var last = await store.Service.GetExpertsAsync(2, 2, null, null);
        Assert.Equal(new[] { 3, 4 }, last.Items.Select(x => x.Id));
        Assert.Null(last.NextAfterId);
    }
    private sealed class Store : IExpertRepository, IUnitOfWork, IPersistenceErrorClassifier
    {
        public Expert? Expert;
        public IReadOnlyList<Expert> Page = [];
        public int Take;
        public bool? FilterActive;
        public RequestSubject? FilterSubject;
        public int Saves, Prepared, Commits, Rollbacks;
        public byte[]? Expected;
        public bool Discarded;
        public Exception? Failure;
        public ExpertManagementService Service => new(this, this, this);
        public static Store Existing() => new() { Expert = new Expert { Id = 7, Name = "Example", Capacity = 5, ActiveRequestCount = 3,
            IsActive = true, RowVersion = Convert.FromBase64String(Version), Specialties = [new ExpertSpecialty { Subject = RequestSubject.Legal }] } };
        public Task AddAsync(Expert expert, CancellationToken cancellationToken = default) { Expert = expert; expert.Id = 7; expert.RowVersion = Convert.FromBase64String(Version); return Task.CompletedTask; }
        public Task<Expert?> GetForManagementAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(Expert?.Id == id ? Expert : null);
        public Task<Expert?> GetManagementViewAsync(int id, CancellationToken cancellationToken = default) => GetForManagementAsync(id, cancellationToken);
        public Task<IReadOnlyList<Expert>> GetManagementPageAsync(int? afterId, int take, bool? isActive, RequestSubject? subject, CancellationToken cancellationToken = default)
        {
            Take = take; FilterActive = isActive; FilterSubject = subject;
            return Task.FromResult<IReadOnlyList<Expert>>(Page.Where(x => x.Id > (afterId ?? 0)).Take(take).ToArray());
        }
        public void PrepareManagementUpdate(Expert expert, byte[] expectedRowVersion, IReadOnlyCollection<RequestSubject>? specialties)
        {
            Prepared++; Expected = expectedRowVersion;
            if (specialties is not null) expert.Specialties = specialties.Select(x => new ExpertSpecialty { Subject = x }).ToList();
        }
        public void DiscardManagementChanges(Expert expert) => Discarded = true;
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { Saves++; return Failure is null ? Task.FromResult(1) : Task.FromException<int>(Failure); }
        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.FromResult<IUnitOfWorkTransaction>(new Transaction(this));
        public bool IsOptimisticConcurrencyConflict(Exception exception) => exception is DbUpdateConcurrencyException;
        public bool IsRequestKeyUniqueViolation(Exception exception) => false;
        public Task<Expert?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Expert?> GetForClosureAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Expert>> GetEligibleExpertsAsync(RequestSubject subject, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> RevalidateForAssignmentAsync(Expert expert, RequestSubject subject, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void DiscardTracking(Expert expert) => throw new NotSupportedException();
        private sealed class Transaction(Store store) : IUnitOfWorkTransaction
        {
            public Task CommitAsync(CancellationToken cancellationToken = default) { store.Commits++; return Task.CompletedTask; }
            public Task RollbackAsync(CancellationToken cancellationToken = default) { store.Rollbacks++; return Task.CompletedTask; }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
