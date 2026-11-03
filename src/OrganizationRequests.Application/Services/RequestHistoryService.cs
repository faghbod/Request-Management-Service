using OrganizationRequests.Application.IServiceContracts;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Application.Mapping;
using OrganizationRequests.Domain.IRepositories;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Application.Services;

// سرویس فراخواننده مسئول مجوز، ذخیره تغییرات و مرز تراکنش است.
public sealed class RequestHistoryService(IRequestActivityHistoryRepository repository) : IRequestHistoryService
{
    #region ثبت رویدادهای درخواست

    public void DiscardPending(int requestId, RequestActivityType eventType) => repository.DiscardPending(requestId, eventType);

    public Task RecordRegistrationAsync(
        ServiceRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var history = request.ToRegistrationHistory(userId);

        return repository.AddAsync(history, cancellationToken);
    }

    public Task RecordAssignmentAsync(
        int requestId,
        int expertId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        var input = new RequestAssignmentHistoryDto(requestId, expertId, occurredAtUtc);
        var history = input.ToEntity();

        return repository.AddAsync(history, cancellationToken);
    }

    public Task RecordClosureAsync(
        int requestId,
        int expertId,
        int userId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        var input = new RequestClosureHistoryDto(requestId, expertId, userId, occurredAtUtc);
        var history = input.ToEntity();

        return repository.AddAsync(history, cancellationToken);
    }

    #endregion

    #region دریافت تاریخچه

    public async Task<IReadOnlyList<RequestHistoryItemDto>> GetRequestHistoryAsync(
        int requestId,
        CancellationToken cancellationToken = default)
    {
        var items = await repository.GetByRequestIdAsync(requestId, cancellationToken);

        return items.Select(item => item.ToRequestHistoryDto()).ToArray();
    }

    public async Task<ExpertHistoryPage> GetExpertHistoryAsync(int expertId, ExpertHistoryCursor? cursor, int pageSize, CancellationToken cancellationToken = default)
    {
        if (expertId <= 0)
            throw new ArgumentOutOfRangeException(nameof(expertId));

        if (pageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(pageSize));

        if (cursor is not null && (cursor.Id <= 0 || cursor.OccurredAtUtc == default))
            throw new ArgumentException("Invalid cursor.", nameof(cursor));

        var (items, hasMore) = await repository.GetByExpertIdAsync(expertId, cursor?.OccurredAtUtc, cursor?.Id, pageSize, cancellationToken);

        return items.ToExpertHistoryPage(hasMore);
    }

    #endregion
}
