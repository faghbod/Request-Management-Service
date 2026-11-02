using System.Text.Json;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Application.Exceptions;
using OrganizationRequests.Application.Repositories;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;
namespace OrganizationRequests.Application.Services;
public sealed class ExpertChangeHistoryService(IExpertChangeHistoryRepository repository) : IExpertChangeHistoryService
{
    public void DiscardPending(Expert expert) => repository.DiscardPending(expert);
    public Task RecordCreatedAsync(Expert expert, int actorId, CancellationToken cancellationToken = default) => Record(expert, null, actorId, cancellationToken);
    public Task RecordUpdatedAsync(Expert expert, ExpertConfigurationSnapshot previous, int actorId, CancellationToken cancellationToken = default) => Record(expert, previous, actorId, cancellationToken);
    private Task Record(Expert expert, ExpertConfigurationSnapshot? previous, int actorId, CancellationToken cancellationToken)
    {
        if (actorId <= 0) throw new AccessDeniedException("A valid authenticated user is required.");
        var current = ExpertConfigurationSnapshot.Capture(expert);
        if (previous == current) return Task.CompletedTask;
        return repository.AddAsync(new ExpertChangeHistory {
            Expert = expert, ExpertId = expert.Id, ChangeType = previous is null ? ExpertChangeType.Created : ExpertChangeType.Updated,
            OldName = previous?.Name, OldCapacity = previous?.Capacity, OldIsActive = previous?.IsActive, OldSpecialtiesJson = previous?.SpecialtiesJson,
            NewName = current.Name, NewCapacity = current.Capacity, NewIsActive = current.IsActive, NewSpecialtiesJson = current.SpecialtiesJson,
            ChangedByUserId = actorId, ChangedAtUtc = DateTime.UtcNow }, cancellationToken);
    }
    public async Task<ExpertChangeHistoryPage> GetExpertChangesAsync(int expertId, ExpertChangeHistoryCursor? cursor, int pageSize, CancellationToken cancellationToken = default)
    {
        if (expertId <= 0 || pageSize is < 1 or > 100 || (cursor is not null && (cursor.Id <= 0 || cursor.ChangedAtUtc == default)))
            throw new ExpertValidationException("Invalid expert history parameters.");
        var (items, more) = await repository.GetByExpertIdAsync(expertId, cursor, pageSize, cancellationToken);
        var mapped = items.Select(x => new ExpertChangeHistoryItemDto(x.Id, x.ExpertId, x.ChangeType, x.OldName, x.NewName,
            x.OldCapacity, x.NewCapacity, x.OldIsActive, x.NewIsActive,
            x.OldSpecialtiesJson is null ? null : Deserialize(x.OldSpecialtiesJson), Deserialize(x.NewSpecialtiesJson), x.ChangedByUserId,
            DateTime.SpecifyKind(x.ChangedAtUtc, DateTimeKind.Utc))).ToArray();
        var last = mapped.LastOrDefault();
        return new(mapped, more && last is not null ? new(last.ChangedAtUtc, last.Id) : null);
    }
    private static RequestSubject[] Deserialize(string json) => JsonSerializer.Deserialize<RequestSubject[]>(json) ?? throw new JsonException("Invalid specialty snapshot.");
}