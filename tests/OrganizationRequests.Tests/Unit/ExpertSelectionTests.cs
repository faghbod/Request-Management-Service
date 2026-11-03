using OrganizationRequests.Domain.IRepositories;
using OrganizationRequests.Application.Services;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;
using Xunit;

namespace OrganizationRequests.Tests.Unit;

// بررسی انتخاب کارشناس بر اساس ظرفیت و بار کاری.
[Trait("Category", "Unit")]
public sealed class ExpertSelectionTests
{
    #region اولویت نسبت و ظرفیت باقی‌مانده

    [Fact]
    public async Task Selects_lowest_load_ratio()
    {
        var service = new ExpertSelectionService(new Candidates(Expert(1, 2, 4), Expert(2, 3, 10)));
        Assert.Equal(2, (await service.SelectBestExpertAsync(RequestSubject.Technical))!.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Equal_ratio_prefers_more_available_capacity_regardless_of_input_order(bool reverse)
    {
        // هر سه نسبت یکسان دارند؛ گزینه ۹ بیشترین جای خالی را دارد.
        var candidates = new[] { Expert(9, 3, 6), Expert(5, 2, 4), Expert(3, 1, 2) };
        var service = new ExpertSelectionService(new Candidates(reverse ? candidates.Reverse().ToArray() : candidates));

        Assert.Equal(9, (await service.SelectBestExpertAsync(RequestSubject.Legal))!.Id);
    }

    [Fact]
    public async Task Idle_experts_prefer_larger_capacity_even_when_active_counts_are_equal()
    {
        // معکوس کردن مقایسه تعداد فعال به‌تنهایی این حالت را پوشش نمی‌دهد.
        var service = new ExpertSelectionService(new Candidates(Expert(1, 0, 2), Expert(9, 0, 10)));

        Assert.Equal(9, (await service.SelectBestExpertAsync(RequestSubject.Technical))!.Id);
    }

    [Fact]
    public async Task More_available_capacity_takes_precedence_over_older_assignment()
    {
        var now = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);
        var older = Expert(1, 1, 2);
        older.AssignedRequests.Add(ActiveRequest(now.AddHours(-5)));
        var moreAvailable = Expert(9, 2, 4);
        moreAvailable.AssignedRequests.Add(ActiveRequest(now.AddMinutes(-1)));
        var service = new ExpertSelectionService(new Candidates(older, moreAvailable));

        Assert.Equal(9, (await service.SelectBestExpertAsync(RequestSubject.Technical))!.Id);
    }

    #endregion

    #region اولویت آخرین درخواست باز

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Equal_load_and_available_capacity_compare_latest_open_assignment_not_oldest(bool reverse)
    {
        var now = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);
        var recentlyAssigned = Expert(1, 2, 4);
        recentlyAssigned.AssignedRequests.Add(ActiveRequest(now.AddHours(-10)));
        recentlyAssigned.AssignedRequests.Add(ActiveRequest(now.AddMinutes(-1)));
        var waitingLonger = Expert(9, 2, 4);
        waitingLonger.AssignedRequests.Add(ActiveRequest(now.AddHours(-3)));
        waitingLonger.AssignedRequests.Add(ActiveRequest(now.AddHours(-2)));

        // موضوع درخواست‌های باز مالی است؛ برای انتخاب فنی نیز زمان آن‌ها باید لحاظ شود.
        var candidates = new[] { recentlyAssigned, waitingLonger };
        var service = new ExpertSelectionService(new Candidates(reverse ? candidates.Reverse().ToArray() : candidates));

        Assert.Equal(9, (await service.SelectBestExpertAsync(RequestSubject.Technical))!.Id);
    }

    [Fact]
    public async Task Closed_and_deleted_requests_do_not_change_last_active_assignment()
    {
        var now = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);
        var waitingLonger = Expert(9, 1, 4);
        waitingLonger.AssignedRequests.Add(ActiveRequest(now.AddHours(-3)));

        var closed = ActiveRequest(now.AddMinutes(-2));
        closed.Status = RequestStatus.Closed;
        closed.ClosedAt = now.AddMinutes(-1);
        waitingLonger.AssignedRequests.Add(closed);

        var deleted = ActiveRequest(now.AddMinutes(-1));
        deleted.IsDeleted = true;
        waitingLonger.AssignedRequests.Add(deleted);

        var recentlyAssigned = Expert(1, 1, 4);
        recentlyAssigned.AssignedRequests.Add(ActiveRequest(now.AddHours(-1)));
        var service = new ExpertSelectionService(new Candidates(recentlyAssigned, waitingLonger));

        Assert.Equal(9, (await service.SelectBestExpertAsync(RequestSubject.Legal))!.Id);
    }

    [Fact]
    public async Task Equal_assignment_times_use_smaller_id_for_a_stable_result()
    {
        var assignedAt = new DateTime(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);
        var first = Expert(9, 1, 4);
        first.AssignedRequests.Add(ActiveRequest(assignedAt));
        var second = Expert(3, 1, 4);
        second.AssignedRequests.Add(ActiveRequest(assignedAt));
        var service = new ExpertSelectionService(new Candidates(first, second));

        Assert.Equal(3, (await service.SelectBestExpertAsync(RequestSubject.Technical))!.Id);
    }

    [Fact]
    public async Task Equally_idle_experts_use_smaller_id_when_no_active_assignment_exists()
    {
        var service = new ExpertSelectionService(new Candidates(Expert(9, 0, 4), Expert(3, 0, 4)));

        Assert.Equal(3, (await service.SelectBestExpertAsync(RequestSubject.Technical))!.Id);
    }

    #endregion

    [Fact]
    public async Task No_candidates_returns_null_and_invalid_subject_is_rejected()
    {
        var service = new ExpertSelectionService(new Candidates());
        Assert.Null(await service.SelectBestExpertAsync(RequestSubject.Financial));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SelectBestExpertAsync((RequestSubject)99));
    }

    private static Expert Expert(int id, int count, int capacity) => new() { Id = id, ActiveRequestCount = count, Capacity = capacity };

    // داده آزمایشی؛ درخواست باز می‌تواند متعلق به تخصص دیگری از همان کارشناس باشد.
    private static ServiceRequest ActiveRequest(DateTime assignedAt) => new()
    {
        Subject = RequestSubject.Financial,
        Status = RequestStatus.Assigned,
        AssignedAt = assignedAt
    };

    private sealed class Candidates(params Expert[] values) : IExpertRepository
    {
        public Task AddAsync(Expert expert, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Expert?> GetForManagementAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Expert?> GetManagementViewAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Expert>> GetManagementPageAsync(int? afterId, int take, bool? isActive, RequestSubject? subject, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void PrepareManagementUpdate(Expert expert, byte[] expectedRowVersion, IReadOnlyCollection<RequestSubject>? specialties) => throw new NotSupportedException();
        public void DiscardManagementChanges(Expert expert) => throw new NotSupportedException();
        public Task<IReadOnlyList<Expert>> GetEligibleExpertsAsync(RequestSubject subject, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Expert>>(values);
        public Task<Expert?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Expert?> GetForClosureAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> RevalidateForAssignmentAsync(Expert expert, RequestSubject subject, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void DiscardTracking(Expert expert) => throw new NotSupportedException();
    }
}
