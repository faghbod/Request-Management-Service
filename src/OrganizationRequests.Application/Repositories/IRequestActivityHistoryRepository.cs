using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Application.Repositories;

public interface IRequestActivityHistoryRepository
{
    Task AddAsync(RequestActivityHistory history, CancellationToken cancellationToken = default);
    void DiscardPending(int requestId, RequestActivityType eventType);
    Task<IReadOnlyList<RequestActivityHistory>> GetByRequestIdAsync(int requestId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<RequestActivityHistory> Items, bool HasMore)> GetByExpertIdAsync(int expertId, ExpertHistoryCursor? cursor, int pageSize, CancellationToken cancellationToken = default);
}
