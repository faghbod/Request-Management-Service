using OrganizationRequests.Domain.Entities;
namespace OrganizationRequests.Domain.IRepositories;
public interface IExpertChangeHistoryRepository
{
    Task AddAsync(ExpertChangeHistory history, CancellationToken cancellationToken = default);
    void DiscardPending(Expert expert);
    Task<(IReadOnlyList<ExpertChangeHistory> Items, bool HasMore)> GetByExpertIdAsync(int expertId, DateTime? beforeUtc, int? beforeId, int pageSize, CancellationToken cancellationToken = default);
}