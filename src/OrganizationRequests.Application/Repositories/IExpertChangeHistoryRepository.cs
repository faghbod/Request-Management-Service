using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Domain.Entities;
namespace OrganizationRequests.Application.Repositories;
public interface IExpertChangeHistoryRepository
{
    Task AddAsync(ExpertChangeHistory history, CancellationToken cancellationToken = default);
    void DiscardPending(Expert expert);
    Task<(IReadOnlyList<ExpertChangeHistory> Items, bool HasMore)> GetByExpertIdAsync(int expertId, ExpertChangeHistoryCursor? cursor, int pageSize, CancellationToken cancellationToken = default);
}