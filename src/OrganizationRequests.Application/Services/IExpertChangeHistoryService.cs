using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;
namespace OrganizationRequests.Application.Services;
public sealed record ExpertConfigurationSnapshot(string Name, int Capacity, bool IsActive, string SpecialtiesJson)
{
    public static ExpertConfigurationSnapshot Capture(Expert expert) => Capture(expert.Name, expert.Capacity, expert.IsActive, expert.Specialties.Select(x => x.Subject));
    public static ExpertConfigurationSnapshot Capture(string name, int capacity, bool active, IEnumerable<RequestSubject> specialties) =>
        new(name, capacity, active, System.Text.Json.JsonSerializer.Serialize(specialties.Select(x => (int)x).Distinct().Order().ToArray()));
}
public interface IExpertChangeHistoryService
{
    Task RecordCreatedAsync(Expert expert, int actorId, CancellationToken cancellationToken = default);
    Task RecordUpdatedAsync(Expert expert, ExpertConfigurationSnapshot previous, int actorId, CancellationToken cancellationToken = default);
    void DiscardPending(Expert expert);
    Task<ExpertChangeHistoryPage> GetExpertChangesAsync(int expertId, ExpertChangeHistoryCursor? cursor, int pageSize, CancellationToken cancellationToken = default);
}