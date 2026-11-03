using Microsoft.EntityFrameworkCore;
using OrganizationRequests.Application.Abstractions;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Application.Exceptions;
using OrganizationRequests.Domain.IRepositories;
using OrganizationRequests.Application.Services;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;
using OrganizationRequests.Domain.UnitOfWork;
using Xunit;

namespace OrganizationRequests.Tests.Unit;

// بررسی ایجاد، ویرایش، اعتبارسنجی و هم‌زمانی کارشناس.
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
    [Fact]
    public async Task Creation_history_captures_actor_and_configuration()
    {
        var store = new Store();
        await store.Service.CreateExpertAsync(Create(subjects: [RequestSubject.Technical, RequestSubject.Financial]));
        var item = Assert.Single(store.Events);
        Assert.Equal(ExpertChangeType.Created, item.ChangeType);
        Assert.Null(item.OldName); Assert.Null(item.OldCapacity); Assert.Null(item.OldIsActive); Assert.Null(item.OldSpecialtiesJson);
        Assert.Equal("Example", item.NewName); Assert.Equal(5, item.NewCapacity); Assert.True(item.NewIsActive);
        Assert.Equal("[1,3]", item.NewSpecialtiesJson); Assert.Equal(42, item.ChangedByUserId);
        Assert.NotEqual(item.ExpertId, item.ChangedByUserId); Assert.Equal(0, store.Expert!.ActiveRequestCount);
    }

    [Fact]
    public async Task Multiple_changes_create_one_immutable_before_and_after_snapshot()
    {
        var store = Store.Existing();
        await store.Service.UpdateExpertAsync(7, Update(8, false, RequestSubject.Technical, RequestSubject.Financial));
        var item = Assert.Single(store.Events);
        Assert.Equal(ExpertChangeType.Updated, item.ChangeType);
        Assert.Equal("Example", item.OldName); Assert.Equal("Renamed", item.NewName);
        Assert.Equal(5, item.OldCapacity); Assert.Equal(8, item.NewCapacity);
        Assert.True(item.OldIsActive); Assert.False(item.NewIsActive);
        Assert.Equal("[2]", item.OldSpecialtiesJson); Assert.Equal("[1,3]", item.NewSpecialtiesJson);
        store.Expert!.Name = "Later"; store.Expert.Specialties.Clear();
        Assert.Equal("Renamed", item.NewName); Assert.Equal("[1,3]", item.NewSpecialtiesJson);
    }

    [Fact]
    public async Task Noop_and_invalid_or_stale_changes_do_not_write_history()
    {
        var store = Store.Existing();
        await store.Service.UpdateExpertAsync(7, new() { Name = " Example ", Capacity = 5, IsActive = true,
            Specialties = [RequestSubject.Legal], ExpectedRowVersion = Version });
        await store.Service.SetExpertActiveStatusAsync(7, new() { IsActive = true, ExpectedRowVersion = Version });
        Assert.Empty(store.Events); Assert.Equal(0, store.Saves); Assert.Equal(0, store.Prepared);
        await Assert.ThrowsAsync<ExpertValidationException>(() => store.Service.UpdateExpertAsync(7, Update(2, true, RequestSubject.Legal)));
        var stale = Update(5, true, RequestSubject.Legal); stale.ExpectedRowVersion = Convert.ToBase64String(new byte[8]);
        await Assert.ThrowsAsync<ExpertConcurrencyException>(() => store.Service.UpdateExpertAsync(7, stale));
        Assert.Empty(store.Events);
    }

    [Fact]
    public async Task Status_and_capacity_changes_create_separate_events()
    {
        var store = Store.Existing();
        await store.Service.SetExpertActiveStatusAsync(7, new() { IsActive = false, ExpectedRowVersion = Version });
        await store.Service.SetExpertActiveStatusAsync(7, new() { IsActive = true, ExpectedRowVersion = Version });
        await store.Service.UpdateExpertAsync(7, Update(3, true, RequestSubject.Legal, RequestSubject.Technical));
        await store.Service.UpdateExpertAsync(7, Update(6, true, RequestSubject.Legal));
        Assert.Equal(4, store.Events.Count);
        Assert.False(store.Events[0].NewIsActive); Assert.True(store.Events[1].NewIsActive);
        Assert.Equal(3, store.Events[2].NewCapacity); Assert.Equal("[2,3]", store.Events[2].NewSpecialtiesJson);
        Assert.Equal(6, store.Events[3].NewCapacity); Assert.Equal("[2]", store.Events[3].NewSpecialtiesJson);
        Assert.Equal(3, store.Expert!.ActiveRequestCount);
    }

    [Theory]
    [InlineData(null)] [InlineData(0)] [InlineData(-1)]
    public async Task Missing_or_invalid_actor_cannot_mutate(int? userId)
    {
        var store = Store.Existing(); store.UserId = userId;
        await Assert.ThrowsAsync<AccessDeniedException>(() => store.Service.CreateExpertAsync(Create(subjects: [RequestSubject.Legal])));
        await Assert.ThrowsAsync<AccessDeniedException>(() => store.Service.UpdateExpertAsync(7, Update(8, true, RequestSubject.Legal)));
        await Assert.ThrowsAsync<AccessDeniedException>(() => store.Service.SetExpertActiveStatusAsync(7, new() { IsActive = false, ExpectedRowVersion = Version }));
        Assert.Equal("Example", store.Expert!.Name); Assert.Empty(store.Events); Assert.Equal(0, store.Saves);
    }

    [Fact]
    public async Task Failed_save_discards_pending_history()
    {
        var store = Store.Existing(); store.Failure = new DbUpdateConcurrencyException();
        await Assert.ThrowsAsync<ExpertConcurrencyException>(() => store.Service.UpdateExpertAsync(7, Update(8, true, RequestSubject.Legal)));
        Assert.Empty(store.Events); Assert.Equal(1, store.Rollbacks);
        var create = new Store { Failure = new ApplicationException() };
        await Assert.ThrowsAsync<ApplicationException>(() => create.Service.CreateExpertAsync(Create(subjects: [RequestSubject.Legal])));
        Assert.Empty(create.Events);
    }

    [Fact]
    public async Task Snapshots_are_deterministic_and_keyset_pages_are_filtered_and_ordered()
    {
        var snapshot = ExpertConfigurationSnapshot.Capture("A", 1, true, [RequestSubject.Technical, RequestSubject.Financial, RequestSubject.Technical]);
        Assert.Equal("[1,3]", snapshot.SpecialtiesJson);
        var store = new Store(); var now = DateTime.UtcNow;
        for (var i = 1; i <= 4; i++) store.Events.Add(new ExpertChangeHistory { Id = i, ExpertId = i == 4 ? 8 : 7,
            NewName = "A", NewCapacity = 1, NewSpecialtiesJson = snapshot.SpecialtiesJson, ChangedAtUtc = now, ChangedByUserId = 42 });
        var service = new ExpertChangeHistoryService(store);
        var first = await service.GetExpertChangesAsync(7, null, 2);
        Assert.Equal(new[] { 3, 2 }, first.Items.Select(x => x.Id));
        Assert.Equal(new[] { RequestSubject.Financial, RequestSubject.Technical }, first.Items[0].NewSpecialties);
        var second = await service.GetExpertChangesAsync(7, first.NextCursor, 2);
        Assert.Equal(new[] { 1 }, second.Items.Select(x => x.Id)); Assert.Null(second.NextCursor);
        await Assert.ThrowsAsync<ExpertValidationException>(() => service.GetExpertChangesAsync(7, null, 101));
    }
    [Fact]
    public async Task Removing_specialty_and_deactivation_change_selection_without_altering_existing_assignment()
    {
        var store = Store.Existing();
        var assigned = new ServiceRequest { Id = 55, AssignedExpertId = 7 };
        store.Expert!.AssignedRequests.Add(assigned);
        var selection = new ExpertSelectionService(store);
        Assert.NotNull(await selection.SelectBestExpertAsync(RequestSubject.Legal));
        await store.Service.UpdateExpertAsync(7, Update(8, true, RequestSubject.Technical));
        Assert.Null(await selection.SelectBestExpertAsync(RequestSubject.Legal));
        Assert.NotNull(await selection.SelectBestExpertAsync(RequestSubject.Technical));
        Assert.Equal(7, assigned.AssignedExpertId);
        Assert.Equal("[2]", store.Events[0].OldSpecialtiesJson);
        Assert.Equal("[3]", store.Events[0].NewSpecialtiesJson);
        await store.Service.SetExpertActiveStatusAsync(7, new() { IsActive = false, ExpectedRowVersion = Version });
        Assert.Null(await selection.SelectBestExpertAsync(RequestSubject.Technical));
        Assert.Equal(3, store.Expert.ActiveRequestCount);
    }
    private sealed class Store : IExpertRepository, IUnitOfWork, IPersistenceErrorClassifier, ICurrentUserContext, IExpertChangeHistoryRepository
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
        public int? UserId { get; set; } = 42;
        public int? ExpertId => 99;
        public List<ExpertChangeHistory> Events = [];
        public ExpertManagementService Service => new(this, this, this, this, new ExpertChangeHistoryService(this));
        public Task AddAsync(ExpertChangeHistory history, CancellationToken cancellationToken = default) { Events.Add(history); return Task.CompletedTask; }
        public void DiscardPending(Expert expert) => Events.RemoveAll(x => ReferenceEquals(x.Expert, expert));
        public Task<(IReadOnlyList<ExpertChangeHistory> Items, bool HasMore)> GetByExpertIdAsync(int expertId, DateTime? beforeUtc, int? beforeId, int pageSize, CancellationToken cancellationToken = default)
        {
            var items = Events.Where(x => x.ExpertId == expertId && (!beforeUtc.HasValue || x.ChangedAtUtc < beforeUtc.Value || (x.ChangedAtUtc == beforeUtc.Value && x.Id < beforeId!.Value))).OrderByDescending(x => x.ChangedAtUtc).ThenByDescending(x => x.Id).Take(pageSize + 1).ToArray();
            return Task.FromResult< (IReadOnlyList<ExpertChangeHistory>, bool)>((items.Take(pageSize).ToArray(), items.Length > pageSize));
        }
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
        public Task<IReadOnlyList<Expert>> GetEligibleExpertsAsync(RequestSubject subject, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Expert>>(Expert is { IsActive: true, IsDeleted: false } expert && expert.Capacity > expert.ActiveRequestCount && expert.Specialties.Any(x => x.Subject == subject) ? [expert] : []);
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
