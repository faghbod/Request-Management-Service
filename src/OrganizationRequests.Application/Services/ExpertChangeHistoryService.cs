using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Application.Exceptions;
using OrganizationRequests.Application.Mapping;
using OrganizationRequests.Domain.IRepositories;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Application.Services;

// ثبت و خواندن تغییرات تنظیمات کارشناس بدون ذخیره مستقل از عملیات اصلی.
public sealed class ExpertChangeHistoryService(IExpertChangeHistoryRepository repository) : IExpertChangeHistoryService
{
    #region ثبت تغییرات

    public void DiscardPending(Expert expert) => repository.DiscardPending(expert);

    public Task RecordCreatedAsync(Expert expert, int actorId, CancellationToken cancellationToken = default) => Record(expert, null, actorId, cancellationToken);

    public Task RecordUpdatedAsync(Expert expert, ExpertConfigurationSnapshot previous, int actorId, CancellationToken cancellationToken = default) => Record(expert, previous, actorId, cancellationToken);

    private Task Record(Expert expert, ExpertConfigurationSnapshot? previous, int actorId, CancellationToken cancellationToken)
    {
        if (actorId <= 0)
            throw new AccessDeniedException("A valid authenticated user is required.");

        var current = ExpertConfigurationSnapshot.Capture(expert);

        // برای تنظیمات یکسان، رویداد تکراری در تاریخچه ایجاد نمی‌شود.
        if (previous == current)
            return Task.CompletedTask;

        return repository.AddAsync(new ExpertChangeHistory
        {
            Expert = expert,
            ExpertId = expert.Id,
            ChangeType = previous is null ? ExpertChangeType.Created : ExpertChangeType.Updated,
            OldName = previous?.Name,
            OldCapacity = previous?.Capacity,
            OldIsActive = previous?.IsActive,
            OldSpecialtiesJson = previous?.SpecialtiesJson,
            NewName = current.Name,
            NewCapacity = current.Capacity,
            NewIsActive = current.IsActive,
            NewSpecialtiesJson = current.SpecialtiesJson,
            ChangedByUserId = actorId,
            ChangedAtUtc = DateTime.UtcNow
        }, cancellationToken);
    }

    #endregion

    #region خواندن تاریخچه

    public async Task<ExpertChangeHistoryPage> GetExpertChangesAsync(int expertId, ExpertChangeHistoryCursor? cursor, int pageSize, CancellationToken cancellationToken = default)
    {
        if (expertId <= 0 || pageSize is < 1 or > 100 || (cursor is not null && (cursor.Id <= 0 || cursor.ChangedAtUtc == default)))
            throw new ExpertValidationException("Invalid expert history parameters.");

        var (items, more) = await repository.GetByExpertIdAsync(expertId, cursor?.ChangedAtUtc, cursor?.Id, pageSize, cancellationToken);
        var mapped = items.Select(x => x.ToDto()).ToArray();
        var last = mapped.LastOrDefault();

        return new(mapped, more && last is not null ? new(last.ChangedAtUtc, last.Id) : null);
    }

    #endregion
}
