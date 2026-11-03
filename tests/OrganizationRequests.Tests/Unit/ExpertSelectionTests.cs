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
    [Fact]
    public async Task Selects_lowest_load_ratio()
    {
        var service = new ExpertSelectionService(new Candidates(Expert(1, 2, 4), Expert(2, 3, 10)));
        Assert.Equal(2, (await service.SelectBestExpertAsync(RequestSubject.Technical))!.Id);
    }

    [Fact]
    public async Task Equal_ratio_prefers_fewer_active_then_smaller_id()
    {
        var service = new ExpertSelectionService(new Candidates(Expert(9, 3, 6), Expert(5, 2, 4), Expert(3, 2, 4)));
        Assert.Equal(3, (await service.SelectBestExpertAsync(RequestSubject.Legal))!.Id);
    }

    [Fact]
    public async Task No_candidates_returns_null_and_invalid_subject_is_rejected()
    {
        var service = new ExpertSelectionService(new Candidates());
        Assert.Null(await service.SelectBestExpertAsync(RequestSubject.Financial));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SelectBestExpertAsync((RequestSubject)99));
    }

    private static Expert Expert(int id, int count, int capacity) => new() { Id = id, ActiveRequestCount = count, Capacity = capacity };

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
